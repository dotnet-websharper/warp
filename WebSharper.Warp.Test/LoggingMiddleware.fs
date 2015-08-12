namespace WebSharper.Warp.Test

open System
open System.Collections.Generic
open System.Threading.Tasks
open System.IO

open Owin
open Microsoft.Owin
open Microsoft.Owin.Logging
open WebSharper.Owin

module Logging = 
    let logRequestBody = false
    let logResponseBody = false

    let simpleRequestKeys = [ "owin.RequestScheme";  "owin.RequestProtocol";  "owin.RequestMethod";  "owin.RequestPathBase";  "owin.RequestPath";    
                              "owin.RequestQueryString"; "server.RemoteIpAddress"; "server.RemotePort";  "server.LocalIpAddress";  "server.LocalPort"]
    let dictionaryRequestKeys =  ["owin.RequestHeaders"; "Microsoft.Owin.Cookies#dictionary"; "Microsoft.Owin.Query#dictionary"]

    let simpleResponseKeys =     ["owin.ResponseStatusCode"; "owin.RequestMethod"; "owin.RequestPath"]
    let dictionaryResponseKeys = ["owin.ResponseHeaders"]   

    type OwinLogger(next: AppFunc, appBuilder: Owin.IAppBuilder) =

        let logEnvValue (logger: ILogger) (dict: IDictionary<string,'a>) key = 
            match dict.TryGetValue(key) with
            | (true, value) -> logger.WriteInformation(String.Format("{0}: {1}", key, value))
            | _ -> () 

        let logDictValues (logger: ILogger) (env: Env) key = 
            match env.TryGetValue(key) with
            | (true, (:? IDictionary<string, string> as dictionary)) -> dictionary.Keys |> Seq.iter (logEnvValue logger dictionary)
            | _ -> () 

        let logRequest (logger: ILogger) env = 
            logger.WriteInformation(String.Format("{0}REQUEST:", Environment.NewLine))
            simpleRequestKeys |> Seq.iter (logEnvValue logger env)
            dictionaryRequestKeys |> Seq.iter (logDictValues logger env)

        let logResponse (logger: ILogger) env =
            logger.WriteInformation(String.Format("{0}RESPONSE:", Environment.NewLine))
            simpleResponseKeys |> Seq.iter (logEnvValue logger env)
            dictionaryResponseKeys |> Seq.iter (logDictValues logger env)

        member this.Invoke(env: Env) =               
            async {
                let logger = appBuilder.CreateLogger<OwinLogger>()
                let context = new OwinContext(env)
                let responseStream = context.Response.Body
                let responseBuffer = new MemoryStream() :> Stream

                logRequest logger env

                if logger.IsEnabled(Diagnostics.TraceEventType.Verbose) then
                    let requestStream = context.Request.Body
                    let buffer = new MemoryStream()
                    do! requestStream.CopyToAsync(buffer) |> Async.AwaitIAsyncResult |> Async.Ignore
                    let inputStreamReader = new StreamReader(buffer)
                    let! requestBody = inputStreamReader.ReadToEndAsync() |> Async.AwaitTask
                    logger.WriteVerbose("Request Body:")
                    logger.WriteVerbose(requestBody)
                    buffer.Seek(0L, SeekOrigin.Begin) |> ignore
                    do! buffer.CopyToAsync(requestStream) |> Async.AwaitIAsyncResult |> Async.Ignore
                    context.Response.Body <- responseBuffer

                do! next.Invoke(env) |> Async.AwaitIAsyncResult |> Async.Ignore

                logResponse logger env

                if logger.IsEnabled(Diagnostics.TraceEventType.Verbose) then
                    responseBuffer.Seek(0L, SeekOrigin.Begin) |> ignore
                    let outputStreamReader = new StreamReader(responseBuffer)
                    let! responseBody = outputStreamReader.ReadToEndAsync() |> Async.AwaitTask
                    logger.WriteVerbose("Response Body:")
                    logger.WriteVerbose(responseBody)
                    responseBuffer.Seek(0L, SeekOrigin.Begin) |> ignore
                    do! responseBuffer.CopyToAsync(responseStream) |> Async.AwaitIAsyncResult |> Async.Ignore

            } |> Async.Ignore |> Async.StartAsTask :> Task

    let logger: MiddlewareGenerator = 
        let loggerFunc (appBuilder: Owin.IAppBuilder) =
            let midFunc(next: AppFunc) = 
                let appFunc (env: Env) = (new OwinLogger(next, appBuilder)).Invoke(env)
                AppFunc(appFunc)
            MidFunc(midFunc)
        MiddlewareGenerator(loggerFunc)
