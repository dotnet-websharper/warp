namespace WebSharper

open System
open System.IO
open System.Reflection
open System.Runtime.CompilerServices
open WebSharper.Sitelets
open global.Owin
open Microsoft.Owin.Hosting
open Microsoft.Owin.StaticFiles
open Microsoft.Owin.FileSystems
open WebSharper
open WebSharper.Owin
open WebSharper.Html.Server

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
        let compiler = FE.Prepare opts (failwithf "%O")
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
        let scripted = defaultArg scripted false
        let asm =
            match assembly with
            | Some a -> a
            | None -> Assembly.GetCallingAssembly()
        match Compilation.compile asm with
        | None -> failwith "Failed to compile with WebSharper"
        | Some (asm, refs) ->
            let rootDir = defaultArg rootDir (Directory.GetCurrentDirectory())
            let url = defaultArg url "http://localhost:9000/"
            let url = if not (url.EndsWith "/") then url + "/" else url
            let debug = defaultArg debug false
            Compilation.outputFiles rootDir refs
            Compilation.outputFile rootDir asm
            try
                let server = WebApp.Start(url, fun appB ->
                    appB.UseStaticFiles(
                            StaticFileOptions(
                                FileSystem = PhysicalFileSystem(rootDir)))
                        .UseCustomSitelet(
                            Options.Create(asm.Info)
                                .WithServerRootDirectory(rootDir)
                                .WithDebug(debug),
                            sitelet)
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
        PageContent (fun ctx ->
            { Page.Default with
                Body = match Body with Some h -> h :> seq<_> | None -> Seq.empty
                Doctype = Doctype
                Head = match Head with Some h -> h :> seq<_> | None -> Seq.empty
                Title = Title
            }
        )

    /// Creates a Warp application based on an Action->Content mapping.
    static member CreateApplication(f: Context<'EndPoints> -> 'EndPoints -> Content<'EndPoints>) : WarpApplication<'EndPoints> =
        Sitelet.InferAsync (fun ctx action -> async.Return (f ctx action))

    /// Creates a Warp single page application (SPA) based on the body of that single page.
    static member CreateSPA (f: Sitelets.Context<SPA.Endpoints> -> #seq<Element>) =
        Warp.CreateApplication (fun ctx SPA.Endpoints.Home ->
            Warp.Page(Body = f ctx)
        )

    /// Creates a Warp single page application (SPA). Use Warp.Page() to create the returned page.
    static member CreateSPA (f: Sitelets.Context<SPA.Endpoints> -> Sitelets.Content<SPA.Endpoints>) =
        Warp.CreateApplication (fun ctx SPA.Endpoints.Home ->
            f ctx
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
