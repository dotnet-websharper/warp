# websharper.warp

[![Gitter](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/intellifactory/websharper.warp?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

WebSharper Warp is a friction-less web development library for building **scripted** and **standalone** **full-stack** F# client-server applications. Warp is built on top of WebSharper and is designed to help you become more productive in a scripted or self-hosted fashion.

**Update**: Note that many Warp shorthands have been integrated into WebSharper now. To use these new shorthands, you don't need Warp, but be sure to open `WebSharper.Sitelets` instead.  (You can find the old getting started guide that uses Warp [here](getting-started.md)).

 * `Warp.Page` -> `Content.Page`
 * `Warp.Text` -> `Content.Text`
 * `Warp.Json` -> `Content.Json`
 * `Warp.CreateSPA` -> `Application.SinglePage`
 * `Warp.CreateApplication` -> `Application.MultiPage`
 
# Installing

To get started with Warp is super-easy, all you need is to open a new F# Console Application (or any other F# project type if you want to script applications), and add `WebSharper.Warp` to it:

```
Install-Package WebSharper.Warp
```

Or if you use [Paket](http://fsprojects.github.io/Paket/):

```
paket init
paket add nuget WebSharper.Warp
```

# Scripting with Warp

When you add the `WebSharper.Warp` NuGet package to your project in Visual Studio, a new document tab will open giving the necessary boilerplate for using Warp in scripted applications.

For instance, the SPA example above can be written as an F# script and executed in F# Interative:

```fsharp
#I "../packages/Owin.1.0/lib/net40"
#I "../packages/Microsoft.Owin.3.0.1/lib/net45"
#I "../packages/Microsoft.Owin.Host.HttpListener.3.0.1/lib/net45"
#I "../packages/Microsoft.Owin.Hosting.3.0.1/lib/net45"
#I "../packages/Microsoft.Owin.FileSystems.3.0.1/lib/net45"
#I "../packages/Microsoft.Owin.StaticFiles.3.0.1/lib/net45"
#I "../packages/WebSharper.3.2.8.170/lib/net40"
#I "../packages/WebSharper.Compiler.3.2.4.170/lib/net40"
#I "../packages/WebSharper.Owin.3.2.6.83/lib/net45"
#load "../packages/WebSharper.Warp.3.2.10.13/tools/reference.fsx"

open WebSharper
open WebSharper.Html.Server

let MySite =
    Warp.CreateSPA (fun ctx ->
        [H1 [Text "Hello world!"]])

do Warp.RunAndWaitForInput(MySite) |> ignore
```

If you use Paket, then you should replace the `#`-lines above with this one:

```fsharp
#load "../packages/WebSharper.Warp/tools/reference-nover.fsx"
```

In FSI, you should see:

```
--> Added 'c:\sandbox\test\Library1\HelloWorld\../packages/Owin.1.0/lib/net40' to library include path
[... more lines ...]

[Loading c:\sandbox\test\Library1\packages\WebSharper.Warp.3.2.10.13\tools\reference.fsx]

namespace FSI_0004

Serving http://localhost:9000/, press Enter to stop.
```

You can then test this application as before:

![](http://i.imgur.com/xYITvCql.png)
