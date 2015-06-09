namespace WebSharper.Warp.Test

open WebSharper

type Action =
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
        Warp.CreateApplication(fun ctx action ->
            match action with
            | Action.Index ->
                Warp.Page(
                    Title = "foo",
                    Body = [
                        Div [Text (System.DateTime.UtcNow.ToString())]
                        Div [ClientSide <@ Client.Content () @>]
                    ]
                )
        )

    [<EntryPoint>]
    let main argv = 
        Warp.Run app
        0 // return an integer exit code
