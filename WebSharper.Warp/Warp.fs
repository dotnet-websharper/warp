namespace WebSharper.Warp

open System
open System.IO
open System.Reflection
open WebSharper.Sitelets
open global.Owin
open Microsoft.Owin.Hosting
open Microsoft.Owin.StaticFiles
open Microsoft.Owin.FileSystems
open WebSharper
open WebSharper.Owin
open WebSharper.Html.Server

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

/// Utilities to work with Warp applications
type Application<'T when 'T : equality> =
    /// Creates a Warp application based on an Action->Content mapping
    static member Create(f: 'T -> Content<'T>) = Sitelet.Infer f

    /// Creates an HTML page response with the specified body
    static member PageWithBody (f: Context<_> -> #seq<Element>) =
        PageContent (fun ctx ->
            { Page.Default with
                Body = f ctx }
        )

    /// Creates an HTML page response with the specified head and body
    static member PageWithHeadAndBody (f: Context<_> -> #seq<Element> * #seq<Element>) =
        PageContent (fun ctx ->
            let head, body = f ctx
            { Page.Default with
                Head = head
                Body = body }
        )

    /// Runs a Warp application
    static member Run(sitelet: Sitelet<'T>, ?debug, ?port, ?rootDir, ?scripted, ?assembly) =
        let scripted = defaultArg scripted false
        let asm =
            match assembly with
            | Some a -> a
            | None ->
                if scripted then
                    Assembly.Load(AssemblyName("FSI-ASSEMBLY"))
                else
                    typeof<'T>.Assembly
        match Compilation.compile asm with
        | None -> eprintfn "Failed to compile with WebSharper"
        | Some (asm, refs) ->
            let rootDir = defaultArg rootDir (Directory.GetCurrentDirectory())
            let port = defaultArg port 9000
            let debug = defaultArg debug true
            Compilation.outputFiles rootDir refs
            Compilation.outputFile rootDir asm
            let url = sprintf "http://localhost:%i/" port
            try
                use server = WebApp.Start(url, fun appB ->
                    appB.UseStaticFiles(
                            StaticFileOptions(
                                FileSystem = PhysicalFileSystem(rootDir)))
                        .UseCustomSitelet(
                            Options.Create(asm.Info)
                                .WithServerRootDirectory(rootDir)
                                .WithDebug(debug),
                            sitelet)
                    |> ignore)
                stdout.WriteLine("Serving {0}, press Enter to stop.", url)
                stdin.ReadLine() |> ignore
            with e ->
                eprintfn "Error starting website:\n%s" e.Message
