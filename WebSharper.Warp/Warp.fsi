namespace WebSharper

open System
open System.Collections.Generic
open System.Reflection
open System.Runtime.CompilerServices
open System.Threading.Tasks
open WebSharper.Html.Server
open WebSharper.Sitelets

[<AutoOpen>]
module Extensions =

    type Page with
        /// Include the given stylesheet to the page.
        member WithStyleSheet : href: string -> Page
        /// Include the given JavaScript to the page.
        member WithJavaScript : href: string -> Page

module SPA =
    type EndPoint =
        | [<EndPoint "GET /">] Home

/// A WebSharper application whose HTTP endpoints are matched to values of type 'EndPoint.
type WarpApplication<'EndPoint when 'EndPoint : equality> = Sitelet<'EndPoint>

[<Extension>]
module Owin =

    /// Warp OWIN middleware options.
    type WarpOptions<'EndPoint when 'EndPoint : equality> =

        /// Create options for a Warp application compiled from the given assembly.
        /// debug: true if the served JavaScript files should be readable, false for compressed.
        /// rootDir: the root directory of the application.
        new : Assembly
            * ?debug: bool
            * ?rootDir: string
            * ?scripted: bool
            -> WarpOptions<'EndPoint>

    /// Warp OWIN middleware.
    type WarpMiddleware<'EndPoint when 'EndPoint : equality> =

        /// next: The next OWIN middleware to run, if any.
        /// app: The Warp application to run.
        /// options: Options that instruct the middleware how to run and where to look for files.
        new : next: Owin.AppFunc
            * app: WarpApplication<'EndPoint>
            * options: WarpOptions<'EndPoint>
            -> WarpMiddleware<'EndPoint>

        /// Invokes the Warp middleware with the provided environment dictionary.
        member Invoke : env: IDictionary<string, obj> -> Task

#if NO_UINEXT
#else
open WebSharper.UI.Next
#endif

/// Utilities to work with Warp applications.
[<Extension; Class>]
type Warp =

    interface IDisposable

    /// Get the URL on which the Warp application is listening.
    member Url : string

    /// Stop the running Warp application.
    member Stop : unit -> unit

    /// Runs the Warp application.
    /// debug: true if the served JavaScript files should be readable, false for compressed.
    /// url: the URL on which to listen; defaults to "http://localhost:9000/".
    /// rootDir: the root directory of the application.
    /// assembly: the main assembly to compile to JavaScript; defaults to the calling assembly.
    static member Run
        : app: WarpApplication<'EndPoint>
        * ?debug: bool
        * ?url: string
        * ?rootDir: string
        * ?scripted: bool
        * ?assembly: Assembly
        -> Warp

    /// Runs the Warp application and waits for standard input.
    /// debug: true if the served JavaScript files should be readable, false for compressed.
    /// url: the URL on which to listen; defaults to "http://localhost:9000/".
    /// rootDir: the root directory of the application.
    /// assembly: the main assembly to compile to JavaScript; defaults to the calling assembly.
    /// Returns: an error code suitable for returning from the application's entry point.
    static member RunAndWaitForInput
        : app: WarpApplication<'EndPoint>
        * ?debug: bool
        * ?url: string
        * ?rootDir: string
        * ?scripted: bool
        * ?assembly: Assembly
        -> int

    /// Creates an HTML page response.
    [<Obsolete "Use Content.Page">]
    static member Page
        : ?Body: #seq<Element>
        * ?Head: #seq<Element>
        * ?Title: string
        * ?Doctype: string
        -> Async<Content<'EndPoint>>

    /// Creates an HTML page from an <html> element.
    static member Page : Element -> Async<Content<'EndPoint>>

#if NO_UINEXT
#else

    /// Creates an HTML page from an <html> `Doc`.
    /// Equivalent to Content.Doc.
    [<Obsolete "Use Content.Doc">]
    static member Doc : Doc -> Async<Content<'EndPoint>>

    /// Creates an HTML page response from `Doc`s.
    [<Obsolete "Use Content.Doc">]
    static member Doc
        : ?Body: #seq<Doc>
        * ?Head: #seq<Doc>
        * ?Title: string
        * ?Doctype: string
        -> Async<Content<'EndPoint>>

#endif

    /// Creates a JSON-encoded content.
    [<Obsolete "Use Content.Json">]
    static member Json : 'Data -> Async<Content<'EndPoint>>

    /// Creates a Warp application based on an `Action->Content` mapping.
    [<Obsolete "Use Application.MultiPage">]
    static member CreateApplication
        : (Context<'EndPoint> -> 'EndPoint -> Async<Content<'EndPoint>>)
        -> WarpApplication<'EndPoint>

    /// Creates a Warp single page application (SPA) based on the body of that single page.
    static member CreateSPA
        : (Context<SPA.EndPoint> -> #seq<Element>)
        -> WarpApplication<SPA.EndPoint>

    /// Creates a Warp single page application (SPA). Use Warp.Page() to create the returned page.
    [<Obsolete "Use Application.SinglePage">]
    static member CreateSPA
        : (Context<SPA.EndPoint> -> Async<Content<SPA.EndPoint>>)
        -> WarpApplication<SPA.EndPoint>

    /// Creates a Warp single page application (SPA) that responds with the given text.
    [<Obsolete "Use Application.Text">]
    static member Text : string -> WarpApplication<SPA.EndPoint>

type ClientAttribute = WebSharper.Pervasives.JavaScriptAttribute
type ServerAttribute = WebSharper.Pervasives.RpcAttribute
type EndPointAttribute = Sitelets.EndPointAttribute
type MethodAttribute = Sitelets.MethodAttribute
type JsonAttribute = Sitelets.JsonAttribute
type QueryAttribute = Sitelets.QueryAttribute
type FormDataAttribute = Sitelets.FormDataAttribute
type WildCardAttribute = Sitelets.WildcardAttribute
