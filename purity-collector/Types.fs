namespace FSharp.PureAnalyzer

open System

/// Origin of a pure-method whitelist entry.
type PureOrigin =
    | Automatic
    | Manual of comment: string option

/// A single method known (or proposed) to be pure.
type PureMethod =
    { FullName: string; Origin: PureOrigin }

/// Official `.pure.json` whitelist document.
type PureFile =
    {
        SchemaVersion: string
        PackageId: string
        PackageVersion: string
        GeneratedAt: DateTimeOffset
        Generator: string
        PureMethods: PureMethod list
    }

/// One counter-evidence / doubt entry (List C – informational only).
type DoubtEntry =
    {
        FullName: string
        SourceList: string
        Reason: string
        EvidenceUrls: string list
        Confidence: string
    }

/// `doubt.pureness.json` document (List C).
type DoubtFile =
    {
        SchemaVersion: string
        GeneratedAt: DateTimeOffset
        Generator: string
        Doubts: DoubtEntry list
    }

/// One entry in `definitely.proven` (excluded from future List C searches).
type ProvenEntry =
    {
        FullName: string
        Reason: string
        AddedAt: DateTimeOffset
    }

/// `definitely.proven` document.
type ProvenFile =
    {
        SchemaVersion: string
        UpdatedAt: DateTimeOffset
        Entries: ProvenEntry list
    }

/// Intermediate representation of a method discovered during List A analysis.
type AnalyzedMethod =
    {
        FullName: string
        AssemblyName: string
        IsPublic: bool
        IsStatic: bool
        HasBody: bool
        /// Direct callees (full names) extracted from IL.
        Callees: string list
        /// True when the method body contains constructs considered impure.
        HasLocalImpurity: bool
        /// Human-readable reasons for local impurity (diagnostics / debugging).
        ImpurityReasons: string list
    }

module Constants =
    [<Literal>]
    let SchemaVersion = "1.0"

    [<Literal>]
    let GeneratorName = "fsharp-pure-analyzer/purity-collector"

    [<Literal>]
    let GeneratorVersion = "0.1.0"

    let Generator = $"{GeneratorName}/{GeneratorVersion}"
