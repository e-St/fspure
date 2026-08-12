namespace Fspure.Cli

open System
open System.IO
open System.Security.Cryptography
open System.Text

/// Content-addressed report cache. Key includes project, filters, analyzer drop, and sources.
module Cache =

    let private hex (bytes: byte[]) =
        Convert.ToHexString(bytes).ToLowerInvariant()

    let private sha256Bytes (bytes: byte[]) =
        SHA256.HashData bytes |> hex

    let private sha256Text (s: string) =
        sha256Bytes (Encoding.UTF8.GetBytes s)

    let private fileStamp (path: string) =
        try
            if File.Exists path then
                let info = FileInfo path
                sprintf "%s|%d|%d" (Paths.normalize (Path.GetFullPath path)) info.Length info.LastWriteTimeUtc.Ticks
            else
                sprintf "missing|%s" (Paths.normalize path)
        with _ ->
            sprintf "err|%s" path

    let private collectSources (projectPath: string) : string list =
        let dir = Paths.projectDirectory projectPath

        try
            Directory.EnumerateFiles(dir, "*.fs", SearchOption.AllDirectories)
            |> Seq.filter (fun p ->
                let n = Paths.normalize p
                not (n.Contains "/obj/") && not (n.Contains "/bin/"))
            |> Seq.map Path.GetFullPath
            |> Seq.sort
            |> Seq.toList
        with _ ->
            []

    let makeKey (opts: AnalyzeOptions) (projectPath: string) (analyzersPath: string option) : string =
        let sb = StringBuilder()

        sb.Append("v1").Append('\n') |> ignore
        sb.Append("project=").Append(fileStamp projectPath).Append('\n') |> ignore
        sb.Append("format=").Append(opts.Format.ToString()).Append('\n') |> ignore
        sb.Append("cfg=").Append(opts.Configuration).Append('\n') |> ignore

        for f in opts.Focus |> List.map Paths.normalize |> List.sort do
            sb.Append("focus=").Append(f).Append('\n') |> ignore

        for i in opts.Ignore |> List.map Paths.normalize |> List.sort do
            sb.Append("ignore=").Append(i).Append('\n') |> ignore

        match analyzersPath with
        | Some p ->
            let dll = Path.Combine(p, "FSharp.PureAnalyzer.dll")
            let dll2 = Path.Combine(p, "dotnet", "fs", "FSharp.PureAnalyzer.dll")

            let stamp =
                if File.Exists dll then fileStamp dll
                elif File.Exists dll2 then fileStamp dll2
                else fileStamp p

            sb.Append("analyzer=").Append(stamp).Append('\n') |> ignore
        | None -> sb.Append("analyzer=none\n") |> ignore

        for src in collectSources projectPath do
            sb.Append("src=").Append(fileStamp src).Append('\n') |> ignore

        sha256Text (sb.ToString())

    let tryGet (cacheDir: string) (key: string) : byte[] option =
        let path = Path.Combine(cacheDir, key + ".out")

        try
            if File.Exists path then Some(File.ReadAllBytes path) else None
        with _ ->
            None

    let put (cacheDir: string) (key: string) (bytes: byte[]) =
        try
            Directory.CreateDirectory cacheDir |> ignore
            let path = Path.Combine(cacheDir, key + ".out")
            File.WriteAllBytes(path, bytes)
        with _ ->
            ()
