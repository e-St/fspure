namespace Fspure.Cli

open System

/// Wire format of `fspure analyze` (JSON or SARIF).
type OutputFormat =
    | Json
    | Sarif

    static member parse (s: string) =
        match s.Trim().ToLowerInvariant() with
        | "json" -> Ok Json
        | "sarif" -> Ok Sarif
        | other -> Error $"unknown --format '{other}' (expected json or sarif)"

    override this.ToString() =
        match this with
        | Json -> "json"
        | Sarif -> "sarif"

/// One analyser diagnostic. PURE001 is a call; PURE002/PURE003 are definitions.
type Diagnostic =
    {
        Code: string
        File: string
        StartLine: int
        StartColumn: int
        EndLine: int
        EndColumn: int
        Message: string
        /// Definition name (PURE002/PURE003) or callee (PURE001).
        FullName: string
        /// Enclosing definition of a PURE001 call. Empty on definitions.
        Caller: string
        /// Impure callee of a PURE001 call. Empty on definitions.
        Callee: string
    }

module Diagnostic =
    let compare (a: Diagnostic) (b: Diagnostic) =
        let c = String.Compare(a.File, b.File, StringComparison.Ordinal)
        if c <> 0 then c
        else
            let c = a.StartLine.CompareTo b.StartLine
            if c <> 0 then c
            else
                let c = a.StartColumn.CompareTo b.StartColumn
                if c <> 0 then c
                else
                    let c = String.Compare(a.Code, b.Code, StringComparison.Ordinal)
                    if c <> 0 then c
                    else String.Compare(a.FullName, b.FullName, StringComparison.Ordinal)

    let sort (items: Diagnostic list) =
        items |> List.sortWith compare

type AnalyzeOptions =
    {
        Project: string option
        Focus: string list
        Ignore: string list
        Format: OutputFormat
        FailOnImpure: bool
        CacheDir: string option
        AnalyzersPath: string option
        FsharpAnalyzers: string option
        SarifInput: string option
        Output: string option
        Configuration: string
        Verbose: bool
    }

module AnalyzeOptions =
    let empty =
        {
            Project = None
            Focus = []
            Ignore = []
            Format = Json
            FailOnImpure = false
            CacheDir = None
            AnalyzersPath = None
            FsharpAnalyzers = None
            SarifInput = None
            Output = None
            Configuration = "Release"
            Verbose = false
        }

module ExitCode =
    /// No focused impure-in-caller calls (or --fail-on-impure was not set).
    [<Literal>]
    let Success = 0

    /// At least one focused impure call inside a function, and --fail-on-impure.
    [<Literal>]
    let Impure = 1

    /// Bad arguments / missing files.
    [<Literal>]
    let Usage = 2

    /// Analyzer host failed (fsharp-analyzers, missing drop, no SARIF).
    [<Literal>]
    let AnalyzeFailed = 3

module Constants =
    [<Literal>]
    let SchemaVersion = "1.1"

    [<Literal>]
    let ToolName = "fspure"

    [<Literal>]
    let ToolVersion = "0.1.0"

    [<Literal>]
    let SchemaId = "https://github.com/e-St/fspure/raw/main/src/fspure/fspure-analyze.schema.json"

    let PurityCodes = set [ "PURE001"; "PURE002"; "PURE003" ]

    [<Literal>]
    let CallCode = "PURE001"

    [<Literal>]
    let ImpureCode = "PURE002"

    [<Literal>]
    let PureCode = "PURE003"
