namespace FSharp.PureAnalyzer

open System
open System.Collections.Generic
open System.IO
open System.Reflection
open System.Text
open System.Text.Json

/// DTO for the embedded foundational.pure.json resource.
[<CLIMutable>]
type PureMethodDto =
    {
        fullName: string
        origin: string
        comment: string
    }

[<CLIMutable>]
type PureFileDto =
    {
        schemaVersion: string
        packageId: string
        packageVersion: string
        generatedAt: string
        generator: string
        pureMethods: PureMethodDto array
    }

/// Cached access to the embedded foundational pure set, with lookup that tolerates
/// FCS vs IL naming differences (generic arity markers, Map vs map, etc.).
module PureSet =

    /// Strip signature suffixes and generic arity markers (`1, `2, …).
    let normalizeName (fullName: string) : string =
        let noSig =
            match fullName.IndexOf '(' with
            | -1 -> fullName
            | i -> fullName.Substring(0, i)

        let builder = StringBuilder(noSig.Length)
        let mutable i = 0

        while i < noSig.Length do
            let c = noSig[i]

            if c = '`' then
                i <- i + 1

                while i < noSig.Length && Char.IsDigit noSig[i] do
                    i <- i + 1
            else
                builder.Append c |> ignore
                i <- i + 1

        builder.ToString()

    /// Last segment after final '.', case-insensitive key for aliasing Map/map.
    let private lastSegmentKey (fullName: string) =
        let n = normalizeName fullName
        let i = n.LastIndexOf '.'
        let typePart = if i < 0 then "" else n.Substring(0, i)
        let memberPart = if i < 0 then n else n.Substring(i + 1)
        typePart.ToLowerInvariant() + "." + memberPart.ToLowerInvariant()

    let private loadResource () =
        let assembly = Assembly.GetExecutingAssembly()

        let resourceName =
            assembly.GetManifestResourceNames()
            |> Array.tryFind (fun n -> n.EndsWith("foundational.pure.json", StringComparison.OrdinalIgnoreCase))

        match resourceName with
        | None -> failwith "Embedded resource 'foundational.pure.json' was not found."
        | Some name ->
            match assembly.GetManifestResourceStream(name) with
            | null -> failwith $"Unable to open embedded resource '%s{name}'."
            | stream ->
                use reader = new StreamReader(stream)
                reader.ReadToEnd()

    type private PureIndex =
        {
            Exact: HashSet<string>
            Normalized: HashSet<string>
            /// type.lower + "." + member.lower → at least one pure method exists
            LastSegment: HashSet<string>
        }

    let private parsedIndex =
        lazy
            let json = loadResource ()

            let options =
                JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)

            match JsonSerializer.Deserialize<PureFileDto>(json, options) with
            | null -> failwith "Failed to deserialize foundational.pure.json."
            | dto ->
                let exact = HashSet<string>(StringComparer.Ordinal)
                let normalized = HashSet<string>(StringComparer.Ordinal)
                let lastSeg = HashSet<string>(StringComparer.Ordinal)

                for method in dto.pureMethods do
                    let fn = method.fullName
                    exact.Add(fn) |> ignore
                    let n = normalizeName fn
                    normalized.Add(n) |> ignore
                    lastSeg.Add(lastSegmentKey fn) |> ignore

                {
                    Exact = exact
                    Normalized = normalized
                    LastSegment = lastSeg
                }

    /// True when the given full name is known pure (exact, normalized, or
    /// case-insensitive last-segment match against the embedded set).
    let contains (fullName: string) : bool =
        let idx = parsedIndex.Value

        if idx.Exact.Contains(fullName) then
            true
        else
            let n = normalizeName fullName

            if idx.Normalized.Contains(n) || idx.Exact.Contains(n) then
                true
            else
                idx.LastSegment.Contains(lastSegmentKey fullName)

    /// Compatibility: expose as IReadOnlySet for call sites that only need Count / enumeration.
    /// Membership should use `contains` for fuzzy matching.
    let knownPure: IReadOnlySet<string> = parsedIndex.Value.Exact
