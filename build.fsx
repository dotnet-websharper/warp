#load "tools/includes.fsx"
open IntelliFactory.Build

let bt =
    BuildTool().PackageId("WebSharper.Warp")
        .VersionFrom("WebSharper")

let main =
    bt.FSharp.Library("WebSharper.Warp")
        .SourcesFromProject()
        .References(fun r ->
            [
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
                r.NuGet("WebSharper.Compiler").Reference()
                r.NuGet("WebSharper.Owin").Reference()
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
                r.NuGet("WebSharper.Compiler").Reference()
                r.NuGet("WebSharper.Owin").Reference()
            ])

bt.Solution [
    main
    test

    bt.NuGet.CreatePackage()
        .Configure(fun c ->
            { c with
                Title = Some "WebSharper.Warp"
                LicenseUrl = Some "http://websharper.com/licensing"
                ProjectUrl = Some "https://github.com/intellifactory/websharper.warp"
                Description = "WebSharper Warp"
                Authors = ["IntelliFactory"]
                RequiresLicenseAcceptance = true })
        .Add(main)
]
|> bt.Dispatch
