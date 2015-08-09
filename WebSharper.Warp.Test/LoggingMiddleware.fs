namespace WebSharper.Warp.Test

open System
open System.Collections.Generic
open System.Threading.Tasks
open System.IO

open Microsoft.Owin
open WebSharper.Owin

module Logging = 
    let logRequestBody = false
    let logResponseBody = false

    let simpleRequestKeys = [ "owin.RequestScheme";  "owin.RequestProtocol";  "owin.RequestMethod";  "owin.RequestPathBase";  "owin.RequestPath";    
                              "owin.RequestQueryString"; "server.RemoteIpAddress"; "server.RemotePort";  "server.LocalIpAddress";  "server.LocalPort"]
    let dictionaryRequestKeys =  ["owin.RequestHeaders"; "Microsoft.Owin.Cookies#dictionary"; "Microsoft.Owin.Query#dictionary"]

    let simpleResponseKeys =     ["owin.ResponseStatusCode"; "owin.RequestMethod"; "owin.RequestPath"]
    let dictionaryResponseKeys = ["owin.ResponseHeaders"]    

    let logEnvValue (dict: IDictionary<string,'a>) key = 
        match dict.TryGetValue(key) with
        | (true, value) -> Console.WriteLine("{0}: {1}", key, value)
        | _ -> () 

    let logDictValues (env: Env) key = 
        match env.TryGetValue(key) with
        | (true, (:? IDictionary<string, string> as dictionary)) -> dictionary.Keys |> Seq.iter (logEnvValue dictionary)
        | _ -> () 

    let logRequest env = 
        Console.WriteLine(String.Format("{0}REQUEST:", Environment.NewLine))
        simpleRequestKeys |> Seq.iter (logEnvValue env)
        dictionaryRequestKeys |> Seq.iter (logDictValues env)

    let logResponse env =
        Console.WriteLine(String.Format("{0}RESPONSE:", Environment.NewLine))
        simpleResponseKeys |> Seq.iter (logEnvValue env)
        dictionaryResponseKeys |> Seq.iter (logDictValues env)

    let toMiddleware (middlewareFunc: AppFunc -> Env -> Task): MidFunc =
        MidFunc(fun next -> AppFunc(middlewareFunc next)) 

    let logger: MidFunc =
        let loggerFunc (next: AppFunc) (env: Env) = 
            async {
                let context = new OwinContext(env)
                let responseStream = context.Response.Body
                let responseBuffer = new MemoryStream() :> Stream

                logRequest env

                if logRequestBody then
                    let requestStream = context.Request.Body
                    let buffer = new MemoryStream()
                    do! requestStream.CopyToAsync(buffer) |> Async.AwaitIAsyncResult |> Async.Ignore
                    let inputStreamReader = new StreamReader(buffer)
                    let! requestBody = inputStreamReader.ReadToEndAsync() |> Async.AwaitTask
                    Console.WriteLine("Request Body:")
                    Console.WriteLine(requestBody)
                    buffer.Seek(0L, SeekOrigin.Begin) |> ignore
                    do! buffer.CopyToAsync(requestStream) |> Async.AwaitIAsyncResult |> Async.Ignore

                if logResponseBody then
                    context.Response.Body <- responseBuffer

                do! next.Invoke(env) |> Async.AwaitIAsyncResult |> Async.Ignore

                logResponse env

                if logResponseBody then
                    responseBuffer.Seek(0L, SeekOrigin.Begin) |> ignore
                    let outputStreamReader = new StreamReader(responseBuffer)
                    let! responseBody = outputStreamReader.ReadToEndAsync() |> Async.AwaitTask
                    Console.WriteLine("Response Body:")
                    Console.WriteLine(responseBody)
                    responseBuffer.Seek(0L, SeekOrigin.Begin) |> ignore
                    do! responseBuffer.CopyToAsync(responseStream) |> Async.AwaitIAsyncResult |> Async.Ignore

            } |> Async.Ignore |> Async.StartAsTask :> Task
        loggerFunc |> toMiddleware
