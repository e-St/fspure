namespace FSharp.PureAnalyzer

open System
open System.Collections.Generic
open System.IO
open System.Reflection
open System.Text.Json

/// DTO for the embedded foundational.pure.json resource.
[<CLIMutable>]
type private PureMethodDto =
    {
        fullName: string
        origin: string
        comment: string
    }

[<CLIMutable>]
type private PureFileDto =
    {
        schemaVersion: string
        packageId: string
        packageVersion: string
        generatedAt: string
        generator: string
        pureMethods: PureMethodDto array
    }

/// Cached access to the embedded foundational pure set.
module PureSet =

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

    let private parsedSet =
        lazy
            let json = loadResource ()

            let options =
                JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)

            match JsonSerializer.Deserialize<PureFileDto>(json, options) with
            | null -> failwith "Failed to deserialize foundational.pure.json."
            | dto ->
                let set = HashSet<string>(StringComparer.Ordinal)

                for method in dto.pureMethods do
                    set.Add(method.fullName) |> ignore

                set

    /// The globally cached known-pure set.
    let knownPure: IReadOnlySet<string> = parsedSet.Value
