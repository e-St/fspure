namespace Fspure.DocsGenerator

open System
open System.IO
open System.Net
open System.Threading

module Serve =

    let private mime =
        dict
            [
                ".html", "text/html; charset=utf-8"
                ".htm", "text/html; charset=utf-8"
                ".css", "text/css; charset=utf-8"
                ".js", "text/javascript; charset=utf-8"
                ".json", "application/json; charset=utf-8"
                ".png", "image/png"
                ".jpg", "image/jpeg"
                ".jpeg", "image/jpeg"
                ".svg", "image/svg+xml"
                ".ico", "image/x-icon"
                ".md", "text/markdown; charset=utf-8"
                ".txt", "text/plain; charset=utf-8"
                ".woff", "font/woff"
                ".woff2", "font/woff2"
            ]

    let private contentType (path: string) =
        let ext =
            match Path.GetExtension path with
            | null
            | "" -> ""
            | e -> e.ToLowerInvariant()

        match mime.TryGetValue ext with
        | true, t -> t
        | _ -> "application/octet-stream"

    let private resolveUnder (root: string) (urlPath: string) : string option =
        let decoded = Uri.UnescapeDataString(urlPath.Split('?', 2).[0])
        let rel = decoded.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)
        let candidate = Path.GetFullPath(Path.Combine(root, rel))
        let rootDir = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)
        let rootPrefix = rootDir + string Path.DirectorySeparatorChar

        if
            not (
                candidate.Equals(rootDir, StringComparison.Ordinal)
                || candidate.StartsWith(rootPrefix, StringComparison.Ordinal)
            )
        then
            None
        elif Directory.Exists candidate then
            Some(Path.Combine(candidate, "index.html"))
        else
            Some candidate

    let private handle (siteRoot: string) (ctx: HttpListenerContext) =
        let req = ctx.Request
        let res = ctx.Response

        try
            let urlPath =
                match req.Url with
                | null -> "/"
                | u -> u.AbsolutePath

            match resolveUnder siteRoot urlPath with
            | Some path when File.Exists path ->
                let bytes = File.ReadAllBytes path
                res.StatusCode <- 200
                res.ContentType <- contentType path
                res.ContentLength64 <- bytes.LongLength
                res.OutputStream.Write(bytes, 0, bytes.Length)
            | _ ->
                res.StatusCode <- 404
                res.ContentType <- "text/plain; charset=utf-8"
                let msg = "404"B
                res.OutputStream.Write(msg, 0, msg.Length)
        finally
            res.OutputStream.Close()

    let private debounce (ms: int) (action: unit -> unit) : unit -> unit =
        let gate = obj ()
        let mutable pending: Timer option = None

        let fire (_: obj | null) =
            lock gate (fun () ->
                match pending with
                | Some t ->
                    t.Dispose()
                    pending <- None
                | None -> ())

            action ()

        fun () ->
            lock gate (fun () ->
                match pending with
                | Some t -> t.Dispose()
                | None -> ()

                pending <- Some(new Timer(TimerCallback fire, null, ms, Timeout.Infinite)))

    let watch (dirs: string list) (onChange: unit -> unit) : IDisposable =
        let fire = debounce 300 onChange
        let watchers = ResizeArray<FileSystemWatcher>()

        for dir in dirs do
            if Directory.Exists dir then
                let w =
                    new FileSystemWatcher(
                        dir,
                        IncludeSubdirectories = true,
                        NotifyFilter =
                            (NotifyFilters.FileName
                             ||| NotifyFilters.DirectoryName
                             ||| NotifyFilters.LastWrite
                             ||| NotifyFilters.Size)
                    )

                let interesting (name: string | null) =
                    let n =
                        match name with
                        | null -> ""
                        | s -> s

                    not (
                        n.EndsWith("~", StringComparison.Ordinal)
                        || n.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
                    )

                let handler (_: obj) (e: FileSystemEventArgs) =
                    if interesting e.Name then
                        fire ()

                w.Changed.AddHandler(FileSystemEventHandler handler)
                w.Created.AddHandler(FileSystemEventHandler handler)
                w.Deleted.AddHandler(FileSystemEventHandler handler)
                w.Renamed.AddHandler(RenamedEventHandler(fun o ev -> handler o ev))
                w.EnableRaisingEvents <- true
                watchers.Add w

        { new IDisposable with
            member _.Dispose() =
                for w in watchers do
                    w.EnableRaisingEvents <- false
                    w.Dispose()
        }

    /// Serve `siteRoot` on loopback. Blocks until Ctrl+C.
    let listen (port: int) (siteRoot: string) : unit =
        let prefix = $"http://127.0.0.1:{port}/"
        let listener = new HttpListener()
        listener.Prefixes.Add prefix
        listener.Start()
        printfn "listening on %s" prefix

        try
            while listener.IsListening do
                let ctx = listener.GetContext()
                handle siteRoot ctx
        finally
            listener.Stop()
            listener.Close()
