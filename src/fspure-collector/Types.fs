namespace FSharp.PureAnalyzer

open System

// PureFile / PureMethod / PureOrigin live in FSharp.PureSchema (single source of truth).
// Collector-specific document types and analysis IR remain here.

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
    /// Re-export of the frozen PureFile schema version for call sites in the collector.
    [<Literal>]
    let SchemaVersion = FSharp.PureSchema.SchemaVersion.Current

    [<Literal>]
    let GeneratorName = "fsharp-pure-analyzer/fspure-collector"

    [<Literal>]
    let GeneratorVersion = "0.1.0"

    let Generator = $"{GeneratorName}/{GeneratorVersion}"
