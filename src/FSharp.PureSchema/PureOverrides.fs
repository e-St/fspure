namespace FSharp.PureSchema

open System
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json
open System.Text.Json.Serialization

/// Project-level purity overrides (scenario 3).
/// File name: fspure.overrides.json (next to the .fsproj, or FSPURE_OVERRIDES_PATH).
///
/// Precedence when applied by the analyser:
///   overrides (add/remove) > library embeds > foundational
type PureOverrides =
    {
        SchemaVersion: string
        /// When false, foundational.pure.json is not used as the base set.
        UseFoundational: bool
        /// Full names to treat as pure (unioned last).
        Add: string list
        /// Full names to strip from the composed set (after library embeds).
        Remove: string list
        /// SHA-256 of the source JSON (cache key fragment).
        ContentHash: string
    }

module PureOverridesSchema =

    [<Literal>]
    let Current = "1.0"

    [<Literal>]
    let FileName = "fspure.overrides.json"

    let Supported: Set<string> = set [ Current ]

    let isSupported (version: string) : bool =
        not (String.IsNullOrWhiteSpace version)
        && Supported.Contains(version.Trim())

type PureOverridesError =
    | InvalidJson of message: string
    | UnsupportedSchemaVersion of version: string
    | MissingRequiredField of fieldName: string
    | InvalidField of fieldName: string * message: string

    override this.ToString() =
        match this with
        | InvalidJson msg -> $"invalid JSON: {msg}"
        | UnsupportedSchemaVersion v -> $"unsupported schemaVersion '{v}' (supported: 1.0)"
        | MissingRequiredField name -> $"missing required field: {name}"
        | InvalidField(name, msg) -> $"invalid field '{name}': {msg}"

/// Load / validate fspure.overrides.json documents.
module PureOverridesIO =

    [<CLIMutable>]
    type PureOverridesDto =
        {
            schemaVersion: string | null
            useFoundational: Nullable<bool>
            add: string array | null
            remove: string array | null
        }

    let private options =
        let o = JsonSerializerOptions(PropertyNameCaseInsensitive = true)
        o.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
        o.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull
        o

    let private contentHash (json: string) : string =
        let bytes = Encoding.UTF8.GetBytes(json)
        Convert.ToHexString(SHA256.HashData bytes).ToLowerInvariant()

    let private cleanNames (arr: string array | null) : string list =
        match arr with
        | null -> []
        | a ->
            a
            |> Array.choose (fun s ->
                if String.IsNullOrWhiteSpace s then None else Some(s.Trim()))
            |> Array.distinct
            |> Array.toList

    /// Parse and validate an overrides document from JSON text.
    let parse (json: string) : Result<PureOverrides, PureOverridesError> =
        if String.IsNullOrWhiteSpace json then
            Error(InvalidJson "empty document")
        else
            try
                match JsonSerializer.Deserialize<PureOverridesDto | null>(json, options) with
                | null -> Error(InvalidJson "deserialized to null")
                | dto ->
                    match dto.schemaVersion with
                    | null
                    | "" -> Error(MissingRequiredField "schemaVersion")
                    | v when not (PureOverridesSchema.isSupported v) -> Error(UnsupportedSchemaVersion v)
                    | v ->
                        let useFoundational =
                            if dto.useFoundational.HasValue then
                                dto.useFoundational.Value
                            else
                                true

                        Ok
                            {
                                SchemaVersion = v.Trim()
                                UseFoundational = useFoundational
                                Add = cleanNames dto.add
                                Remove = cleanNames dto.remove
                                ContentHash = contentHash json
                            }
            with ex ->
                Error(InvalidJson ex.Message)

    /// Load overrides from a filesystem path.
    let load (path: string) : Result<PureOverrides, PureOverridesError> =
        if String.IsNullOrWhiteSpace path then
            Error(InvalidJson "path is empty")
        elif not (File.Exists path) then
            Error(InvalidJson $"file not found: {path}")
        else
            try
                File.ReadAllText path |> parse
            with ex ->
                Error(InvalidJson ex.Message)

    /// True when the path's file name is the conventional overrides file.
    let isOverridesFileName (path: string) : bool =
        if String.IsNullOrWhiteSpace path then
            false
        else
            match Path.GetFileName path with
            | null
            | "" -> false
            | name -> name.Equals(PureOverridesSchema.FileName, StringComparison.OrdinalIgnoreCase)
