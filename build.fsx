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
                r.NuGet("WebSharper").At(
                    [
                        "lib/net40/WebSharper.Core.dll"
                        "lib/net40/WebSharper.Core.JavaScript.dll"
                        "lib/net40/WebSharper.JavaScript.dll"
                        "lib/net40/WebSharper.Main.dll"
                        "lib/net40/WebSharper.Html.Server.dll"
                        "lib/net40/WebSharper.Html.Client.dll"
                        "lib/net40/WebSharper.Sitelets.dll"
                        "lib/net40/WebSharper.Web.dll"
                        "tools/net40/IntelliFactory.Core.dll"
                        "tools/net40/Mono.Cecil.dll"
                        "tools/net40/WebSharper.Compiler.dll"
                    ]).Reference()
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
                r.NuGet("WebSharper").At(
                    [
                        "lib/net40/WebSharper.Core.dll"
                        "lib/net40/WebSharper.Core.JavaScript.dll"
                        "lib/net40/WebSharper.JavaScript.dll"
                        "lib/net40/WebSharper.Main.dll"
                        "lib/net40/WebSharper.Html.Server.dll"
                        "lib/net40/WebSharper.Html.Client.dll"
                        "lib/net40/WebSharper.Sitelets.dll"
                        "lib/net40/WebSharper.Web.dll"
                        "tools/net40/IntelliFactory.Core.dll"
                        "tools/net40/Mono.Cecil.dll"
                        "tools/net40/WebSharper.Compiler.dll"
                    ]).Reference()
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
                RequiresLicenseAcceptance = true })
        .Add(main)
]
|> bt.Dispatch
