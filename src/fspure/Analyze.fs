namespace Fspure.Cli

open System
open System.IO

/// Orchestrate: cache → host fsharp-analyzers → filter → deterministic document.
module Analyze =

    type RunResult =
        {
            Bytes: byte[]
            Diagnostics: Diagnostic list
            ImpureCount: int
            PureCount: int
            ExitCode: int
            CacheHit: bool
        }

    let private projectRel (opts: AnalyzeOptions) (projectPath: string option) =
        match projectPath with
        | None -> ""
        | Some p ->
            let cwd = Directory.GetCurrentDirectory()
            Paths.relativeTo cwd (Path.GetFullPath p)

    let private resolveProject (opts: AnalyzeOptions) : Result<string option, string> =
        match opts.Project with
        | Some p when File.Exists p -> Ok(Some(Path.GetFullPath p))
        | Some p -> Error $"project not found: {p}"
        | None ->
            match opts.SarifInput with
            | Some _ -> Ok None
            | None -> Error "--project is required unless --sarif is provided"

    let private cacheDirOf (opts: AnalyzeOptions) =
        match opts.CacheDir with
        | Some d when not (String.IsNullOrWhiteSpace d) -> Some(Path.GetFullPath d)
        | _ ->
            match Environment.GetEnvironmentVariable "FSPURE_CACHE_DIR" with
            | null
            | "" -> None
            | d -> Some(Path.GetFullPath d)

    let private loadDiagnostics (opts: AnalyzeOptions) (projectPath: string option) (verbose: bool) : Result<Diagnostic list * bool, string> =
        // Resolve relative SARIF URIs against the project directory, then
        // expose paths relative to cwd so --focus src/Core works from the repo root.
        let cwd = Directory.GetCurrentDirectory()

        let resolveDir =
            match projectPath with
            | Some p -> Paths.projectDirectory p
            | None -> cwd

        match opts.SarifInput with
        | Some sarif ->
            match SarifRead.loadRel resolveDir cwd sarif with
            | Error e -> Error e
            | Ok items -> Ok(items, false)
        | None ->
            match projectPath with
            | None -> Error "--project is required when --sarif is omitted"
            | Some proj ->
                match Host.tryFindAnalyzersPath opts.AnalyzersPath with
                | None ->
                    Error
                        "FSharp.PureAnalyzer not found. Pass --analyzers-path, set FSPURE_ANALYZERS_PATH, or install the FSharp.PureAnalyzer package."
                | Some analyzers ->
                    if verbose then
                        eprintfn "analyzers-path: %s" analyzers

                    let cacheDir = cacheDirOf opts

                    match Host.resolveFsharpAnalyzers opts.FsharpAnalyzers cacheDir verbose with
                    | Error e -> Error e
                    | Ok(exe, prefix) ->
                        let work =
                            match cacheDir with
                            | Some d -> Path.Combine(d, "work")
                            | None -> Path.Combine(Path.GetTempPath(), "fspure-analyze")

                        Directory.CreateDirectory work |> ignore
                        let sarifOut = Path.Combine(work, "analyze.sarif")

                        match Host.runAnalyzers proj analyzers opts.Configuration sarifOut exe prefix verbose with
                        | Error e -> Error e
                        | Ok() ->
                            match SarifRead.loadRel resolveDir cwd sarifOut with
                            | Error e -> Error e
                            | Ok items -> Ok(items, false)

    let run (opts: AnalyzeOptions) : Result<RunResult, string> =
        match resolveProject opts with
        | Error e -> Error e
        | Ok projectPath ->
            let cacheDir = cacheDirOf opts
            let analyzers = Host.tryFindAnalyzersPath opts.AnalyzersPath
            let projectShown = projectRel opts projectPath

            let tryCache =
                match cacheDir, projectPath, opts.SarifInput with
                | Some dir, Some proj, None ->
                    let key = Cache.makeKey opts proj analyzers

                    match Cache.tryGet dir key with
                    | Some bytes -> Some(dir, key, bytes)
                    | None -> Some(dir, key, [||])
                | _ -> None

            match tryCache with
            | Some(_dir, _key, bytes) when bytes.Length > 0 ->
                let callN, callerN =
                    match opts.Format with
                    | Json ->
                        try
                            use doc = System.Text.Json.JsonDocument.Parse(bytes)
                            let sum = doc.RootElement.GetProperty "summary"
                            sum.GetProperty("impureCalls").GetInt32(), sum.GetProperty("affectedCallers").GetInt32()
                        with _ ->
                            0, 0
                    | Sarif ->
                        match SarifRead.parse (Directory.GetCurrentDirectory()) (System.Text.Encoding.UTF8.GetString bytes) with
                        | Ok items -> Report.summaryOf items
                        | Error _ -> 0, 0

                let exit =
                    if opts.FailOnImpure && callN > 0 then
                        ExitCode.Impure
                    else
                        ExitCode.Success

                Ok
                    {
                        Bytes = bytes
                        Diagnostics = []
                        ImpureCount = callN
                        PureCount = callerN
                        ExitCode = exit
                        CacheHit = true
                    }
            | _ ->
                match loadDiagnostics opts projectPath opts.Verbose with
                | Error e -> Error e
                | Ok(raw, _) ->
                    let filtered = Filter.apply opts.Focus opts.Ignore raw |> Diagnostic.sort
                    let bytes = Report.render opts projectShown filtered
                    let callN, callerN = Report.summaryOf filtered

                    match tryCache with
                    | Some(dir, key, _) -> Cache.put dir key bytes
                    | None -> ()

                    let exit =
                        if opts.FailOnImpure && callN > 0 then
                            ExitCode.Impure
                        else
                            ExitCode.Success

                    Ok
                        {
                            Bytes = bytes
                            Diagnostics = filtered
                            ImpureCount = callN
                            PureCount = callerN
                            ExitCode = exit
                            CacheHit = false
                        }
