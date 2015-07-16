namespace WebSharper

open System
open System.Collections.Generic
open System.IO
open System.Reflection
open System.Runtime.CompilerServices
open System.Threading.Tasks
open WebSharper.Sitelets
open global.Owin
open Microsoft.Owin.Hosting
open Microsoft.Owin.StaticFiles
open Microsoft.Owin.FileSystems
open WebSharper
open WebSharper.Owin
open WebSharper.Html.Server

[<AutoOpen>]
module Extensions =
    open WebSharper.Html.Server

    type Sitelets.Page with
        member this.WithStyleSheet (href: string) =
            let css = Link [Rel "stylesheet"; HRef href]
            { this with
                Head = Seq.append this.Head [css]
            }

        member this.WithJavaScript (href: string) =
            let css = Script [Type "text/javascript"; Src href]
            { this with
                Head = Seq.append this.Head [css]
            }

module SPA =
    type Endpoints =
        | [<EndPoint "GET /">] Home
    
module internal Compilation =

    module PC = WebSharper.PathConventions

    open System.Reflection
    open IntelliFactory.Core
    module FE = WebSharper.Compiler.FrontEnd

    let compile asm =
        let localDir = Directory.GetCurrentDirectory()
        let websharperDir = Path.GetDirectoryName typeof<Sitelet<_>>.Assembly.Location
        let fsharpDir = Path.GetDirectoryName typeof<option<_>>.Assembly.Location
        let loadPaths =
            [
                localDir
                websharperDir
                fsharpDir
            ]
        let loader =
            let aR =
                AssemblyResolver.Create()
                    .SearchDirectories(loadPaths)
                    .WithBaseDirectory(fsharpDir)
            FE.Loader.Create aR stderr.WriteLine
        let refs =
            [
                for dll in Directory.EnumerateFiles(websharperDir, "*.dll") do
                    if Path.GetFileName(dll) <> "FSharp.Core.dll" then
                        yield dll
                let dontRef (n: string) =
                    [
                        "FSharp.Compiler.Interactive.Settings,"
                        "FSharp.Compiler.Service,"
                        "FSharp.Core,"
                        "FSharp.Data.TypeProviders,"
                        "Mono.Cecil"
                        "mscorlib,"
                        "System."
                        "System,"
                    ] |> List.exists n.StartsWith
                let rec loadRefs (asms: Assembly[]) (loaded: Map<string, Assembly>) =
                    let refs =
                        asms
                        |> Seq.collect (fun asm -> asm.GetReferencedAssemblies())
                        |> Seq.map (fun n -> n.FullName)
                        |> Seq.distinct
                        |> Seq.filter (fun n -> not (dontRef n || Map.containsKey n loaded))
                        |> Seq.choose (fun n ->
                            try Some (AppDomain.CurrentDomain.Load n)
                            with _ -> None)
                        |> Array.ofSeq
                    if Array.isEmpty refs then
                        loaded
                    else
                        (loaded, refs)
                        ||> Array.fold (fun loaded r -> Map.add r.FullName r loaded)
                        |> loadRefs refs
                let asms =
                    AppDomain.CurrentDomain.GetAssemblies()
                    |> Array.filter (fun a -> not (dontRef a.FullName))
                yield! asms
                |> Array.map (fun asm -> asm.FullName, asm)
                |> Map.ofArray
                |> loadRefs asms
                |> Seq.choose (fun (KeyValue(n, asm)) ->
                    try Some asm.Location
                    with :? NotSupportedException ->
                        // The dynamic assembly does not support `.Location`.
                        // No problem, if it's from the dynamic assembly then
                        // it doesn't incur a dependency anyway.
                        None)
            ]
            |> Seq.distinctBy Path.GetFileName
            |> Seq.map loader.LoadFile
            |> Seq.toList
        let opts = { FE.Options.Default with References = refs }
        let compiler = FE.Prepare opts (eprintfn "%O")
        compiler.Compile(<@ () @>, context = asm)
        |> Option.map (fun asm -> asm, refs)

    let outputFiles root (refs: Compiler.Assembly list) =
        let pc = PC.PathUtility.FileSystem(root)
        let writeTextFile path contents =
            Directory.CreateDirectory (Path.GetDirectoryName path) |> ignore
            File.WriteAllText(path, contents)
        let writeBinaryFile path contents =
            Directory.CreateDirectory (Path.GetDirectoryName path) |> ignore
            File.WriteAllBytes(path, contents)
        let emit text path =
            match text with
            | Some text -> writeTextFile path text
            | None -> ()
        let script = PC.ResourceKind.Script
        let content = PC.ResourceKind.Content
        for a in refs do
            let aid = PC.AssemblyId.Create(a.FullName)
            emit a.ReadableJavaScript (pc.JavaScriptPath aid)
            emit a.CompressedJavaScript (pc.MinifiedJavaScriptPath aid)
            let writeText k fn c =
                let p = pc.EmbeddedPath(PC.EmbeddedResource.Create(k, aid, fn))
                writeTextFile p c
            let writeBinary k fn c =
                let p = pc.EmbeddedPath(PC.EmbeddedResource.Create(k, aid, fn))
                writeBinaryFile p c
            for r in a.GetScripts() do
                writeText script r.FileName r.Content
            for r in a.GetContents() do
                writeBinary content r.FileName (r.GetContentData())

    let (+/) x y = Path.Combine(x, y)

    let outputFile root (asm: FE.CompiledAssembly) =
        let dir = root +/ "Scripts" +/ "WebSharper"
        Directory.CreateDirectory(dir) |> ignore
        File.WriteAllText(dir +/ "WebSharper.EntryPoint.js", asm.ReadableJavaScript)
        File.WriteAllText(dir +/ "WebSharper.EntryPoint.min.js", asm.CompressedJavaScript)

type WarpApplication<'EndPoint when 'EndPoint : equality> = Sitelet<'EndPoint>

[<Extension>]
module Owin =

    /// Warp OWIN middleware options.
    type WarpOptions<'EndPoint when 'EndPoint : equality>(assembly, ?debug, ?rootDir, ?scripted) =
        let debug = defaultArg debug false
        let rootDir = defaultArg rootDir (Directory.GetCurrentDirectory())
        let scripted = defaultArg scripted false

        member this.Assembly = assembly
        member this.Debug = debug
        member this.RootDir = rootDir
        member this.Scripted = scripted

    /// Warp OWIN middleware.
    /// next: The next OWIN middleware to run, if any.
    /// sitelet: The Warp application to run.
    /// options: Options that instruct the middleware how to run and where to look for files.
    type WarpMiddleware<'EndPoint when 'EndPoint : equality>(next: Owin.AppFunc, sitelet: WarpApplication<'EndPoint>, options: WarpOptions<'EndPoint>) =
        let exec =
            // Compile only when the app starts and not on every request.
            match Compilation.compile options.Assembly with
            | None -> failwith "Failed to compile with WebSharper"
            | Some (asm, refs) ->
                Compilation.outputFiles options.RootDir refs
                Compilation.outputFile options.RootDir asm
                // OWIN middleware form a Russian doll, innermost -> outermost
                let siteletMw =
                    WebSharper.Owin.SiteletMiddleware(
                        next, 
                        Options.Create(asm.Info)
                            .WithServerRootDirectory(options.RootDir)
                            .WithDebug(options.Debug),
                        sitelet)
                let staticFilesMw =
                    Microsoft.Owin.StaticFiles.StaticFileMiddleware(
                        AppFunc siteletMw.Invoke,
                        StaticFileOptions(
                            FileSystem = PhysicalFileSystem(options.RootDir)))
                staticFilesMw.Invoke

        /// Invokes the Warp middleware with the provided environment dictionary.
        member this.Invoke(env: IDictionary<string, obj>) = exec env

    /// Adds the Warp middleware to an Owin.IAppBuilder pipeline.
    [<Extension>]
    let UseWarp(appB: Owin.IAppBuilder, sitelet: WarpApplication<'EndPoint>, options) =
        Owin.MidFunc(fun next ->
            let mw = WarpMiddleware<_>(next, sitelet, options)
            Owin.AppFunc mw.Invoke)
        |> appB.Use

open WebSharper.UI.Next

/// Utilities to work with Warp applications.
[<Extension>]
type Warp internal (url: string, stop: unit -> unit) =

    /// Get the URL on which the Warp application is listening.
    member this.Url = url

    /// Stop the running Warp application.
    member this.Stop() = stop()

    interface System.IDisposable with
        member this.Dispose() = stop()

    /// Runs the Warp application.
    [<Extension>]
    static member Run(sitelet: WarpApplication<'EndPoint>, ?debug, ?url, ?rootDir, ?scripted, ?assembly) =
        let assembly =
            match assembly with
            | Some a -> a
            | None -> Assembly.GetCallingAssembly()
        let url = defaultArg url "http://localhost:9000/"
        let url = if not (url.EndsWith "/") then url + "/" else url
        let options = Owin.WarpOptions<_>(assembly, ?debug = debug, ?rootDir = rootDir, ?scripted = scripted)
        try
            let server = WebApp.Start(url, fun appB ->
                Owin.UseWarp(appB, sitelet, options)
                |> ignore)
            new Warp(url, server.Dispose)
        with e ->
            failwithf "Error starting website:\n%s" (e.ToString())

    /// Runs the Warp application and waits for standard input.
    [<Extension>]
    static member RunAndWaitForInput(app: WarpApplication<'EndPoint>, ?debug, ?url, ?rootDir, ?scripted, ?assembly) =
        try
            let assembly =
                match assembly with
                | Some a -> a
                | None -> Assembly.GetCallingAssembly()
            let warp = Warp.Run(app, ?debug = debug, ?url = url, ?rootDir = rootDir, ?scripted = scripted, assembly = assembly)
            stdout.WriteLine("Serving {0}, press Enter to stop.", warp.Url)
            stdin.ReadLine() |> ignore
            warp.Stop()
            0
        with e ->
            eprintfn "%A" e
            1

    /// Creates an HTML page response.
    static member Page (?Body: #seq<Element>, ?Head: #seq<Element>, ?Title: string, ?Doctype: string) =
        Content.PageContent <| fun _ ->
            { Page.Default with
                Body = match Body with Some h -> h :> seq<_> | None -> Seq.empty
                Doctype = Doctype
                Head = match Head with Some h -> h :> seq<_> | None -> Seq.empty
                Title = Title
            }

    /// Creates an HTML page from an <html> element.
    static member Page (doc: Element) =
        Content.WithTemplate (Content.Template.FromHtmlElement doc) ignore

    /// Creates an HTML page from an <html> `Doc`.
    static member Doc (doc: Doc) = Server.Doc.AsContent doc

    /// Creates an HTML page response from `Doc`'s.
    static member Doc (?Body: #seq<Doc>, ?Head: #seq<Doc>, ?Title: string, ?Doctype: string) =
        Content.PageContent <| fun _ ->
            { Page.Default with
                Body =
                    match Body with
                    | Some h ->
                        h :> seq<_>
                        |> Seq.map Server.Doc.AsElements
                        |> Seq.concat
                    | None -> Seq.empty
                Doctype = Doctype
                Head =
                    match Head with
                    | Some h ->
                        h :> seq<_>
                        |> Seq.map Server.Doc.AsElements
                        |> Seq.concat
                    | None -> Seq.empty
                Title = Title
            }

    /// Creates a JSON content.
    static member Json data =
        Content.JsonContent <| fun _ -> data

    /// Creates a Warp application based on an `Action->Content` mapping.
    static member CreateApplication(f: Context<'EndPoints> -> 'EndPoints -> Content<'EndPoints>) : WarpApplication<'EndPoints> =
        Sitelet.InferAsync (fun ctx action -> async.Return (f ctx action))

    /// Creates a Warp single page application (SPA) based on the body of that single page.
    static member CreateSPA (f: Sitelets.Context<SPA.Endpoints> -> #seq<Element>) =
        Warp.CreateApplication (fun ctx SPA.Endpoints.Home ->
            Warp.Page(Body = f ctx)
        )

    /// Creates a Warp single page application (SPA). Use `Warp.Page()` to create the page returned.
    static member CreateSPA (f: Sitelets.Context<SPA.Endpoints> -> Sitelets.Content<SPA.Endpoints>) =
        Warp.CreateApplication (fun ctx SPA.Endpoints.Home ->
            f ctx
        )

    /// Creates a Warp single page application (SPA).
    static member CreateSPA (f: Sitelets.Context<SPA.Endpoints> -> Sitelets.Page) =
        Warp.CreateApplication (fun ctx SPA.Endpoints.Home ->
            PageContent f
        )

    /// Creates a Warp single page application (SPA) that responds with the given text.
    static member Text out =
        Warp.CreateApplication (fun ctx SPA.Endpoints.Home ->
            Warp.Page(Body = [Text out])
        )


type ClientAttribute = WebSharper.Pervasives.JavaScriptAttribute
type ServerAttribute = WebSharper.Pervasives.RpcAttribute
type EndPointAttribute = Sitelets.EndPointAttribute
type MethodAttribute = Sitelets.MethodAttribute
type JsonAttribute = Sitelets.JsonAttribute
type QueryAttribute = Sitelets.QueryAttribute
type FormDataAttribute = Sitelets.FormDataAttribute
type WildCardAttribute = Sitelets.WildcardAttribute

type Page = Sitelets.Page
