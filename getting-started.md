# Hello world!

The simplest Warp site just serves text and consist of a single endpoint (`/`), by default listening on `http://localhost:9000`.

```fsharp
open WebSharper

let MyApp = Warp.Text "Hello world!"

[<EntryPoint>]
do Warp.RunAndWaitForInput(MyApp) |> ignore
```

![](http://i.imgur.com/fZgqeKjl.png)

# Single Page Applications

While serving text is fun and often useful, going beyond isn't any complicated. Warp also helps constructing HTML.  In the most basic form, you can create single page applications (SPAs) using `Warp.CreateSPA` and WebSharper's server-side HTML combinators:

```fsharp
open WebSharper.Html.Server

let MySite =
    Warp.CreateSPA (fun ctx ->
        [H1 [Text "Hello world!"]])

[<EntryPoint>]
do Warp.RunAndWaitForInput(MySite) |> ignore
```

![](http://i.imgur.com/xYITvCql.png)

# Multi-page applications

Using multiple `EndPoints` and `Warp.CreateApplication`, you can define multi-page Warp applications.  When constructing the actual pages, `Warp.Page` comes handy - allowing you to fill the `Title`, `Head`, and the `Body` parts on demand.  `Warp.Page` pages are fully autonomous and will **automatically contain the dependencies of any client-side code used on the page**.

```fsharp
type Endpoints =
    | [<EndPoint "GET /">] Home
    | [<EndPoint "GET /about">] About

let MySite =
    Warp.CreateApplication (fun ctx endpoint ->
        let (=>) label endpoint = A [HRef (ctx.Link endpoint)] -< [Text label]
        match endpoint with
        | Endpoints.Home ->
            Warp.Page(
                Body =
                    [
                        H1 [Text "Hello world!"]
                        "About" => Endpoints.About
                    ]
            )
        | Endpoints.About ->
            Warp.Page(
                Body =
                    [
                        P [Text "This is a simple app"]
                        "Home" => Endpoints.Home
                    ]
            )
    )

[<EntryPoint>]
do Warp.RunAndWaitForInput(MySite) |> ignore
```

![](http://i.imgur.com/WMnmzIPl.png)

# Adding client-side functionality

Warp applications can easily incorporate client-side content and functionality, giving an absolute edge over any web development library. The example below is reimplemented from [Deploying WebSharper apps to Azure via GitHub](http://websharper.com/blog-entry/4368), and although it omits the more advanced templating in that approach (which is straightforward to add to this implementation), it greatly simplifies constructing and running the application.

```fsharp
module Server =
    [<Server>]
    let DoWork (s: string) = 
        async {
            return System.String(List.ofSeq s |> List.rev |> Array.ofList)
        }

[<Client>]
module Client =
    open WebSharper.JavaScript
    open WebSharper.Html.Client

    let Main () =
        let input = Input [Attr.Value ""]
        let output = H1 []
        Div [
            input
            Button [Text "Send"]
            |>! OnClick (fun _ _ ->
                async {
                    let! data = Server.DoWork input.Value
                    output.Text <- data
                }
                |> Async.Start
            )
            HR []
            H4 [Class "text-muted"] -- Text "The server responded:"
            Div [Class "jumbotron"] -< [output]
        ]

let MySite =
    Warp.CreateSPA (fun ctx ->
        [
            H1 [Text "Say Hi to the server"]
            Div [ClientSide <@ Client.Main() @>]
        ])

[<EntryPoint>]
do Warp.RunAndWaitForInput(MySite) |> ignore
```

![](http://i.imgur.com/9sPa4lzl.png)

# Taking things further

Creating RESTful applications, using client-side visualizations is just as easy. For a quick example, here is a Chart.js-based visualization using the `WebSharper.ChartJs` WebSharper extension:

```fsharp
[<Client>]
module Client =
    open WebSharper.JavaScript
    open WebSharper.Html.Client
    open WebSharper.ChartJs

    let RadarChart () =
        Div [
            H3 [Text "Activity Chart"]
            Canvas [Attr.Width  "450"; Attr.Height "300"]
            |>! OnAfterRender (fun canvas ->
                let canvas = As<CanvasElement> canvas.Dom
                RadarChartData(
                    Labels   = [| "Eating"; "Drinking"; "Sleeping";
                                  "Designing"; "Coding"; "Cycling"; "Running" |],
                    Datasets = [|
                        RadarChartDataset(
                            FillColor   = "rgba(151, 187, 205, 0.2)",
                            StrokeColor = "rgba(151, 187, 205, 1)",
                            PointColor  = "rgba(151, 187, 205, 1)",
                            Data        = [|28.0; 48.0; 40.0; 19.0; 96.0; 27.0; 100.0|]
                        )
                        RadarChartDataset(
                            FillColor   = "rgba(220, 220, 220, 0.2)",
                            StrokeColor = "rgba(220, 220, 220, 1)",
                            PointColor  = "rgba(220,220,220,1)",
                            Data        = [|65.0; 59.0; 90.0; 81.0; 56.0; 55.0; 40.0|]
                        )
                    |]
                )
                |> Chart(canvas.GetContext "2d").Radar
                |> ignore
            )
        ]

let MySite =
    Warp.CreateSPA (fun ctx ->
        [
            H1 [Text "Charts are easy with WebSharper Warp!"]
            Div [ClientSide <@ Client.RadarChart() @>]
        ])

[<EntryPoint>]
do Warp.RunAndWaitForInput(MySite) |> ignore
```

![](http://i.imgur.com/9o7x2b1l.png)

