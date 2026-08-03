namespace FSharp.PureSchema

open System

/// Origin of a pure-method whitelist entry.
type PureOrigin =
    | Automatic
    | Manual of comment: string option

/// A single method known (or proposed) to be pure.
type PureMethod =
    { FullName: string; Origin: PureOrigin }

/// Official `.pure.json` whitelist document.
/// This is the single source of truth for the PureFile wire format (schemaVersion 1.0).
type PureFile =
    {
        SchemaVersion: string
        PackageId: string
        PackageVersion: string
        GeneratedAt: DateTimeOffset
        Generator: string
        PureMethods: PureMethod list
    }

/// Schema version constants understood by this library.
module SchemaVersion =

    /// The only schema version currently produced and accepted.
    [<Literal>]
    let Current = "1.0"

    /// Versions this codebase can load. Reject anything not in this set
    /// (including newer versions we do not understand yet).
    let Supported: Set<string> = set [ Current ]

    let isSupported (version: string) : bool =
        not (String.IsNullOrWhiteSpace version)
        && Supported.Contains(version.Trim())

/// Errors raised when loading or validating a PureFile document.
type PureFileError =
    | InvalidJson of message: string
    | MissingRequiredField of fieldName: string
    | UnsupportedSchemaVersion of version: string
    | InvalidField of fieldName: string * message: string

    override this.ToString() =
        match this with
        | InvalidJson msg -> $"invalid JSON: {msg}"
        | MissingRequiredField name -> $"missing required field: {name}"
        | UnsupportedSchemaVersion v ->
            let known =
                SchemaVersion.Supported
                |> Set.toList
                |> String.concat ", "

            $"unsupported schemaVersion '{v}' (supported: {known})"
        | InvalidField(name, msg) -> $"invalid field '{name}': {msg}"

module PureFileError =
    let toMessage (e: PureFileError) : string = e.ToString()
