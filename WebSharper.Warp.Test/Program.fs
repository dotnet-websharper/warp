namespace WebSharper.Warp.Test

open WebSharper
open WebSharper.Sitelets

type EndPoints =
    | [<EndPoint "/">] Index

[<JavaScript>]
module Client =

    open WebSharper.Html.Client
    open WebSharper.JavaScript

    let Content () =
        Button [Text "Click me!"]
        |>! OnClick (fun _ _ ->
            JS.Alert "Clicked!")

module Server =

    open WebSharper.Html.Server
    open WebSharper.Warp.Test.Logging

    let app =
        Application.MultiPage(fun ctx endpoint ->
            match endpoint with
            | EndPoints.Index ->
                Content.Page(
                    Title = "Welcome to my site!",
                    Body = [
                        Div [Text (System.DateTime.UtcNow.ToString())]
                        Div [ClientSide <@ Client.Content () @>]
                    ]
                )
        )

    [<EntryPoint>]
    let main argv = 
        Warp.RunAndWaitForInput(app, urls = ["http://localhost:9000"; "http://127.0.0.1:9000"], before = [Logging.logger])
