module Fspure.Cli.Program

open System
open System.CommandLine
open System.CommandLine.Invocation
open Fspure.Cli

let private stringList (arr: string array | null) =
    match arr with
    | null -> []
    | a -> a |> Array.toList |> List.filter (fun s -> not (String.IsNullOrWhiteSpace s))

let private optString (v: string | null) =
    match v with
    | null
    | "" -> None
    | s when String.IsNullOrWhiteSpace s -> None
    | s -> Some s

let private boolOrDefault (pr: System.CommandLine.Parsing.ParseResult) (opt: Option<bool>) (fallback: bool) =
    match pr.FindResultFor opt with
    | null -> fallback
    | r -> r.GetValueOrDefault<bool>()

let runAnalyze (opts: AnalyzeOptions) : int =
    match Analyze.run opts with
    | Error msg ->
        eprintfn "error: %s" msg
        ExitCode.Usage
    | Ok result ->
        if opts.Verbose then
            eprintfn
                "impureCalls=%d affectedCallers=%d cache=%s"
                result.ImpureCount
                result.PureCount
                (if result.CacheHit then "hit" else "miss")

        try
            Report.writeTo opts.Output result.Bytes
            result.ExitCode
        with ex ->
            eprintfn "error: failed to write output: %s" ex.Message
            ExitCode.Usage

[<EntryPoint>]
let main argv =
    let projectOpt =
        Option<string>(
            aliases = [| "-p"; "--project" |],
            description = "Path to the .fsproj to analyze"
        )

    let focusOpt =
        Option<string[]>(
            aliases = [| "--focus" |],
            description = "Restrict diagnostics to these dirs, files, or globs (repeatable)"
        )

    focusOpt.AllowMultipleArgumentsPerToken <- true

    let ignoreOpt =
        Option<string[]>(
            aliases = [| "--ignore" |],
            description = "Exclude dirs, files, or globs after --focus (repeatable)"
        )

    ignoreOpt.AllowMultipleArgumentsPerToken <- true

    let formatOpt =
        Option<string>(
            aliases = [| "--format" |],
            description = "Output format: json (default) or sarif",
            getDefaultValue = fun () -> "json"
        )

    let failOnImpureOpt =
        Option<bool>(
            aliases = [| "--fail-on-impure" |],
            description = "Exit 1 when any impure call remains inside a focused function"
        )

    let cacheDirOpt =
        Option<string>(
            aliases = [| "--cache-dir" |],
            description = "Cache filtered reports (also FSPURE_CACHE_DIR)"
        )

    let analyzersOpt =
        Option<string>(
            aliases = [| "--analyzers-path" |],
            description = "Folder containing FSharp.PureAnalyzer.dll (or analyzers/dotnet/fs)"
        )

    let fsharpAnalyzersOpt =
        Option<string>(
            aliases = [| "--fsharp-analyzers" |],
            description = "Path to the fsharp-analyzers executable"
        )

    let sarifOpt =
        Option<string>(
            aliases = [| "--sarif" |],
            description = "Reuse an existing fsharp-analyzers SARIF instead of hosting a run"
        )

    let outputOpt =
        Option<string>(
            aliases = [| "-o"; "--output" |],
            description = "Write the document to this path (default: stdout)"
        )

    let configurationOpt =
        Option<string>(
            aliases = [| "--configuration"; "-c" |],
            description = "Build configuration passed to fsharp-analyzers",
            getDefaultValue = fun () -> "Release"
        )

    let verboseOpt =
        Option<bool>(
            aliases = [| "--verbose"; "-v" |],
            description = "Progress on stderr (stdout stays the document only)"
        )

    let analyze =
        Command(
            "analyze",
            "Run FSharp.PureAnalyzer and emit the impure calls found inside functions \
(caller, callee, range) as deterministic JSON or SARIF. \
Uses the same pure-set composition as the editor (foundational + library embeds + overrides)."
        )

    analyze.AddOption projectOpt
    analyze.AddOption focusOpt
    analyze.AddOption ignoreOpt
    analyze.AddOption formatOpt
    analyze.AddOption failOnImpureOpt
    analyze.AddOption cacheDirOpt
    analyze.AddOption analyzersOpt
    analyze.AddOption fsharpAnalyzersOpt
    analyze.AddOption sarifOpt
    analyze.AddOption outputOpt
    analyze.AddOption configurationOpt
    analyze.AddOption verboseOpt

    analyze.SetHandler(fun (ctx: InvocationContext) ->
        let pr = ctx.ParseResult

        let formatRaw =
            match pr.GetValueForOption<string>(formatOpt) with
            | null
            | "" -> "json"
            | s -> s

        match OutputFormat.parse formatRaw with
        | Error msg ->
            eprintfn "error: %s" msg
            ctx.ExitCode <- ExitCode.Usage
        | Ok fmt ->
            let cfg =
                match pr.GetValueForOption<string>(configurationOpt) with
                | null
                | "" -> "Release"
                | s -> s

            let opts =
                { AnalyzeOptions.empty with
                    Project = optString (pr.GetValueForOption<string>(projectOpt))
                    Focus = stringList (pr.GetValueForOption<string[]>(focusOpt))
                    Ignore = stringList (pr.GetValueForOption<string[]>(ignoreOpt))
                    Format = fmt
                    FailOnImpure = boolOrDefault pr failOnImpureOpt false
                    CacheDir = optString (pr.GetValueForOption<string>(cacheDirOpt))
                    AnalyzersPath = optString (pr.GetValueForOption<string>(analyzersOpt))
                    FsharpAnalyzers = optString (pr.GetValueForOption<string>(fsharpAnalyzersOpt))
                    SarifInput = optString (pr.GetValueForOption<string>(sarifOpt))
                    Output = optString (pr.GetValueForOption<string>(outputOpt))
                    Configuration = cfg
                    Verbose = boolOrDefault pr verboseOpt false
                }

            ctx.ExitCode <- runAnalyze opts)

    let root =
        RootCommand(
            "fspure — agent CLI. Subcommand: analyze. \
Collector remains `fspure-collector`. See src/docs/AGENT.md."
        )

    root.AddCommand analyze

    try
        root.Invoke argv
    with ex ->
        eprintfn $"error: unhandled exception: {ex.Message}"
        ExitCode.AnalyzeFailed
