#load "tools/includes.fsx"
open IntelliFactory.Build

let bt =
    BuildTool().PackageId("WebSharper.Warp")
        .VersionFrom("WebSharper")
        .WithFSharpVersion(FSharpVersion.FSharp30)
        .WithFramework(fun fw -> fw.Net45)

let main =
    bt.FSharp.Library("WebSharper.Warp")
        .SourcesFromProject()
        .References(fun r ->
            [
                r.NuGet("Owin").ForceFoundVersion().Reference()
                r.NuGet("Microsoft.Owin").ForceFoundVersion().Reference()
                r.NuGet("Microsoft.Owin.Diagnostics").ForceFoundVersion().Reference()
                r.NuGet("Microsoft.Owin.FileSystems").ForceFoundVersion().Reference()
                r.NuGet("Microsoft.Owin.Host.HttpListener").ForceFoundVersion().Reference()
                r.NuGet("Microsoft.Owin.Hosting").ForceFoundVersion().Reference()
                r.NuGet("Microsoft.Owin.SelfHost").ForceFoundVersion().Reference()
                r.NuGet("Microsoft.Owin.StaticFiles").ForceFoundVersion().Reference()
                r.Assembly("System.Web")
                r.NuGet("WebSharper").ForceFoundVersion().Reference()
                r.NuGet("WebSharper.Html").ForceFoundVersion().Reference()
                r.NuGet("WebSharper.UI.Next").ForceFoundVersion().Reference()
                r.NuGet("WebSharper.Compiler").ForceFoundVersion().Reference()
                r.NuGet("WebSharper.Owin").ForceFoundVersion().Reference()
            ])

let test =
    bt.FSharp.ConsoleExecutable("WebSharper.Warp.Test")
        .SourcesFromProject()
        .References(fun r ->
            [
                r.Project(main)
                r.NuGet("Owin").Reference()
                r.NuGet("Microsoft.Owin").Reference()
                r.NuGet("Microsoft.Owin.Diagnostics").Reference()
                r.NuGet("Microsoft.Owin.FileSystems").Reference()
                r.NuGet("Microsoft.Owin.Host.HttpListener").Reference()
                r.NuGet("Microsoft.Owin.Hosting").Reference()
                r.NuGet("Microsoft.Owin.SelfHost").Reference()
                r.NuGet("Microsoft.Owin.StaticFiles").Reference()
                r.Assembly("System.Web")
                r.NuGet("WebSharper").Reference()
                r.NuGet("WebSharper.Html").Reference()
                r.NuGet("WebSharper.UI.Next").Reference()
                r.NuGet("WebSharper.Compiler").Reference()
                r.NuGet("WebSharper.Owin").Reference()
            ])

let package = bt.NuGet.CreatePackage()

do
    let getVersion (pkg: string) =
        match bt.NuGetResolver.FindLatestVersion(pkg) with
        | None -> failwith ""
        | Some v -> v.ToString()
    let replaceVersion pkg (s: string) =
        s.Replace("${" + pkg + "}", pkg + "." + getVersion pkg)
    let readme =
        System.IO.File.ReadAllText("readme.txt.in")
            .Replace("${WarpVersion}", package.GetComputedVersion())
        |> replaceVersion "WebSharper"
        |> replaceVersion "WebSharper.Compiler"
        |> replaceVersion "Owin"
        |> replaceVersion "Microsoft.Owin"
        |> replaceVersion "Microsoft.Owin.Host.HttpListener"
        |> replaceVersion "Microsoft.Owin.Hosting"
        |> replaceVersion "Microsoft.Owin.SelfHost"
        |> replaceVersion "Microsoft.Owin.FileSystems"
        |> replaceVersion "Microsoft.Owin.StaticFiles"
        |> replaceVersion "WebSharper.Owin"
    System.IO.File.WriteAllText("readme.txt", readme)

bt.Solution [
    main
    test

    package
        .Configure(fun c ->
            { c with
                Title = Some "WebSharper.Warp"
                LicenseUrl = Some "http://websharper.com/licensing"
                ProjectUrl = Some "https://github.com/intellifactory/websharper.warp"
                Description = "WebSharper Warp"
                Authors = ["IntelliFactory"]
                RequiresLicenseAcceptance = true })
        .Add(main)
        .AddFile("readme.txt", "readme.txt")
        .AddFile("WebSharper.Warp/reference.fsx", "tools/reference.fsx")
        .AddFile("WebSharper.Warp/reference-nover.fsx", "tools/reference-nover.fsx")
]
|> bt.Dispatch
