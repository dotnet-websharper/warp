namespace WebSharper.Warp.Test

open System
open System.Collections.Generic
open System.Threading.Tasks
open System.IO

open Owin
open Microsoft.Owin
open Microsoft.Owin.Logging
open Microsoft.Owin.Host.HttpListener.RequestProcessing
open WebSharper.Owin
open WebSharper

module Logging = 

    type OwinLogger(next: AppFunc, appBuilder: Owin.IAppBuilder) =

        let logKeyValuePair (logger: ILogger) (pair: KeyValuePair<string, string>) = 
            logger.WriteInformation(String.Format("\t{0}: {1}", pair.Key, pair.Value))
        
        let convertArrayValues (pair: KeyValuePair<string, array<string>>) =
            new KeyValuePair<string, string>(pair.Key, String.Join(",", pair.Value))

        let logDictValues (logger: ILogger) (env: Env) (envKey: string) = 
            logger.WriteVerbose(String.Format("{0}:", envKey))
            match env.TryGetValue(envKey) with
            | (true, (:? IDictionary<string, array<string>> as dictionary)) -> dictionary 
                                                                                |> Seq.map convertArrayValues  
                                                                                |> Seq.iter (logKeyValuePair logger)
            | (true, (:? IDictionary<string, string> as dictionary)) -> dictionary 
                                                                                |> Seq.iter (logKeyValuePair logger)
            | _ -> ()
        
        let defaultValue value result = 
            match result with
            | (true, resultValue) -> resultValue.ToString()
            | _ -> value 

        let requestToString time (dict: IDictionary<string,'a>) = 
            let remoteIpAddress    = dict.TryGetValue("server.RemoteIpAddress")  |> defaultValue ""
            let remotePort         = dict.TryGetValue("server.RemotePort")       |> defaultValue ""
            let protocol           = dict.TryGetValue("owin.RequestProtocol")    |> defaultValue ""
            let localIP            = dict.TryGetValue("server.LocalIpAddress")   |> defaultValue ""
            let localPort          = dict.TryGetValue("server.LocalPort")        |> defaultValue ""
            let requestMethod      = dict.TryGetValue("owin.RequestMethod")      |> defaultValue ""
            let requestPath        = dict.TryGetValue("owin.RequestPath")        |> defaultValue ""
            let requestQueryString = dict.TryGetValue("owin.RequestQueryString") |> defaultValue ""

            String.Format("{0} {1}:{2} {3} {4} {5} {6} from {7}:{8}", 
                            time, localIP, localPort, requestMethod, requestPath, requestQueryString, 
                            protocol, remoteIpAddress, remotePort ) 

        let logRequest (logger: ILogger) env = 
            logger.WriteVerbose("Request:")
            requestToString DateTime.Now env |> logger.WriteInformation
            logDictValues logger env "owin.RequestHeaders"

        let responseToString time (dict: IDictionary<string,'a>) = 
            let requestMethod   = dict.TryGetValue("owin.RequestMethod")      |> defaultValue ""
            let requestPath     = dict.TryGetValue("owin.RequestPath")        |> defaultValue ""
            let responseStatus  = dict.TryGetValue("owin.ResponseStatusCode") |> defaultValue ""

            String.Format("{0} STATUS {1} for {2} {3}", time, responseStatus, requestMethod, requestPath)

        let logResponse (logger: ILogger) env =
            logger.WriteVerbose("Response:")
            responseToString DateTime.Now env |> logger.WriteInformation
            logDictValues logger env "owin.ResponseHeaders"
        
        member this.Invoke(env: Env) =               
            async {
                let logger = appBuilder.CreateLogger<OwinLogger>()
                let context = new OwinContext(env)
                let responseStream = context.Response.Body
                let responseBuffer = new MemoryStream() :> Stream

                logRequest logger env

                if logger.IsEnabled(Diagnostics.TraceEventType.Verbose) then
                    let requestStream = context.Request.Body
                    let requestBuffer = new MemoryStream()
                    do! requestStream.CopyToAsync(requestBuffer) |> Async.AwaitIAsyncResult |> Async.Ignore
                    requestBuffer.Seek(0L, SeekOrigin.Begin) |> ignore
                    let inputStreamReader = new StreamReader(requestBuffer)
                    let! requestBody = inputStreamReader.ReadToEndAsync() |> Async.AwaitTask
                    logger.WriteVerbose("Request Body:")
                    logger.WriteVerbose(requestBody)
                    requestBuffer.Seek(0L, SeekOrigin.Begin) |> ignore
                    context.Request.Body <- requestBuffer
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

            } |> Async.Ignore 
              |> Async.StartAsTask :> Task

    let logger: MiddlewareGenerator = 
        let loggerFunc (appBuilder: Owin.IAppBuilder) =
            let midFunc(next: AppFunc) = 
                let appFunc (env: Env) = (new OwinLogger(next, appBuilder)).Invoke(env)
                AppFunc(appFunc)
            MidFunc(midFunc)
        MiddlewareGenerator(loggerFunc)

