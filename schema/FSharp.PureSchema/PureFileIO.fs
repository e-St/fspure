namespace FSharp.PureSchema

open System
open System.Collections.Generic
open System.IO
open System.Text.Json
open System.Text.Json.Serialization

/// JSON (de)serialisation, validation, and merge for PureFile documents.
module PureFileIO =

    [<CLIMutable>]
    type PureMethodDto =
        {
            fullName: string | null
            origin: string | null
            comment: string | null
        }

    [<CLIMutable>]
    type PureFileDto =
        {
            schemaVersion: string | null
            packageId: string | null
            packageVersion: string | null
            generatedAt: string | null
            generator: string | null
            pureMethods: PureMethodDto array | null
        }

    let private options =
        let o = JsonSerializerOptions(WriteIndented = true)
        o.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
        o.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull
        o

    let private requireNonEmpty (field: string) (value: string | null) : Result<string, PureFileError> =
        match value with
        | null -> Error(MissingRequiredField field)
        | s when String.IsNullOrWhiteSpace s -> Error(MissingRequiredField field)
        | s -> Ok(s.Trim())

    let private parseOrigin (origin: string | null) (comment: string | null) : Result<PureOrigin, PureFileError> =
        match origin with
        | null
        | "" -> Error(MissingRequiredField "origin")
        | o when o.Equals("automatic", StringComparison.OrdinalIgnoreCase) -> Ok Automatic
        | o when o.Equals("manual", StringComparison.OrdinalIgnoreCase) ->
            let c =
                match comment with
                | null
                | "" -> None
                | s when String.IsNullOrWhiteSpace s -> None
                | s -> Some s

            Ok(Manual c)
        | other -> Error(InvalidField("origin", $"expected 'automatic' or 'manual', got '{other}'"))

    let private dtoToPureMethod (dto: PureMethodDto) : Result<PureMethod, PureFileError> =
        match requireNonEmpty "fullName" dto.fullName with
        | Error e -> Error e
        | Ok fullName ->
            match parseOrigin dto.origin dto.comment with
            | Error e -> Error e
            | Ok origin -> Ok { FullName = fullName; Origin = origin }

    /// Validate a DTO and convert it to the domain PureFile.
    /// Rejects unknown / newer schema versions and missing required fields.
    let validateDto (dto: PureFileDto) : Result<PureFile, PureFileError> =
        match requireNonEmpty "schemaVersion" dto.schemaVersion with
        | Error e -> Error e
        | Ok schemaVersion ->
            if not (SchemaVersion.isSupported schemaVersion) then
                Error(UnsupportedSchemaVersion schemaVersion)
            else
                match requireNonEmpty "packageId" dto.packageId with
                | Error e -> Error e
                | Ok packageId ->
                    match requireNonEmpty "packageVersion" dto.packageVersion with
                    | Error e -> Error e
                    | Ok packageVersion ->
                        match requireNonEmpty "generator" dto.generator with
                        | Error e -> Error e
                        | Ok generator ->
                            match requireNonEmpty "generatedAt" dto.generatedAt with
                            | Error e -> Error e
                            | Ok generatedAtStr ->
                                match DateTimeOffset.TryParse(generatedAtStr) with
                                | false, _ ->
                                    Error(
                                        InvalidField(
                                            "generatedAt",
                                            $"not a valid date-time: '{generatedAtStr}'"
                                        )
                                    )
                                | true, generatedAt ->
                                    match dto.pureMethods with
                                    | null -> Error(MissingRequiredField "pureMethods")
                                    | methodsRaw ->
                                        let rec convert acc i =
                                            if i >= methodsRaw.Length then
                                                Ok(List.rev acc)
                                            else
                                                match dtoToPureMethod methodsRaw[i] with
                                                | Error e -> Error e
                                                | Ok m -> convert (m :: acc) (i + 1)

                                        match convert [] 0 with
                                        | Error e -> Error e
                                        | Ok pureMethods ->
                                            Ok
                                                {
                                                    SchemaVersion = schemaVersion
                                                    PackageId = packageId
                                                    PackageVersion = packageVersion
                                                    GeneratedAt = generatedAt
                                                    Generator = generator
                                                    PureMethods = pureMethods
                                                }

    let private originToDto (origin: PureOrigin) : string * string option =
        match origin with
        | Automatic -> "automatic", None
        | Manual None -> "manual", None
        | Manual(Some c) -> "manual", Some c

    let pureMethodToDto (m: PureMethod) : PureMethodDto =
        let origin, comment = originToDto m.Origin

        {
            fullName = m.FullName
            origin = origin
            comment =
                match comment with
                | None -> null
                | Some c -> c
        }

    let pureFileToDto (file: PureFile) : PureFileDto =
        {
            schemaVersion = file.SchemaVersion
            packageId = file.PackageId
            packageVersion = file.PackageVersion
            generatedAt = file.GeneratedAt.ToString("o")
            generator = file.Generator
            pureMethods = file.PureMethods |> List.map pureMethodToDto |> Array.ofList
        }

    /// Parse and validate a PureFile from a JSON string.
    let parse (json: string) : Result<PureFile, PureFileError> =
        if String.IsNullOrWhiteSpace json then
            Error(InvalidJson "empty document")
        else
            try
                match JsonSerializer.Deserialize<PureFileDto | null>(json, options) with
                | null -> Error(InvalidJson "deserialized to null")
                | dto -> validateDto dto
            with ex ->
                Error(InvalidJson ex.Message)

    /// Load and validate a PureFile from a filesystem path.
    let load (path: string) : Result<PureFile, PureFileError> =
        if String.IsNullOrWhiteSpace path then
            Error(InvalidJson "path is empty")
        elif not (File.Exists path) then
            Error(InvalidJson $"file not found: {path}")
        else
            try
                File.ReadAllText path |> parse
            with ex ->
                Error(InvalidJson ex.Message)

    /// Serialize a PureFile to indented camelCase JSON.
    let serialize (file: PureFile) : string =
        let dto = pureFileToDto file
        JsonSerializer.Serialize(dto, options)

    /// Write a PureFile to disk (creates parent directories as needed).
    let write (path: string) (file: PureFile) : unit =
        let json = serialize file

        match Path.GetDirectoryName path with
        | null
        | "" -> ()
        | dir -> Directory.CreateDirectory(dir) |> ignore

        File.WriteAllText(path, json)

    /// Union pureMethods from the given files in order.
    /// When the same fullName appears more than once, the last file wins.
    /// Metadata (packageId, packageVersion, generator, schemaVersion) is taken from
    /// the first file; generatedAt is set to UtcNow.
    /// Returns Error if the sequence is empty.
    let merge (files: PureFile seq) : Result<PureFile, string> =
        let list = files |> Seq.toList

        match list with
        | [] -> Error "merge requires at least one PureFile"
        | head :: _ ->
            let byName = Dictionary<string, PureMethod>(StringComparer.Ordinal)

            for file in list do
                for m in file.PureMethods do
                    if not (String.IsNullOrWhiteSpace m.FullName) then
                        byName[m.FullName] <- m

            let methods =
                byName.Values
                |> Seq.sortBy (fun m -> m.FullName)
                |> Seq.toList

            Ok
                {
                    SchemaVersion = head.SchemaVersion
                    PackageId = head.PackageId
                    PackageVersion = head.PackageVersion
                    GeneratedAt = DateTimeOffset.UtcNow
                    Generator = head.Generator
                    PureMethods = methods
                }

    /// Convenience: merge base file with zero or more additional files (last wins).
    let mergeWith (baseFile: PureFile) (additional: PureFile list) : PureFile =
        match merge (baseFile :: additional) with
        | Ok f -> f
        | Error msg -> failwith msg
