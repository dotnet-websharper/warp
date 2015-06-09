namespace WebSharper.Warp.Test

open WebSharper

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

    let app =
        Warp.CreateApplication(fun ctx endpoint ->
            match endpoint with
            | EndPoints.Index ->
                Warp.Page(
                    Title = "Welcome to my site!",
                    Body = [
                        Div [Text (System.DateTime.UtcNow.ToString())]
                        Div [ClientSide <@ Client.Content () @>]
                    ]
                )
        )

    [<EntryPoint>]
    let main argv = 
        Warp.RunAndWaitForInput app
