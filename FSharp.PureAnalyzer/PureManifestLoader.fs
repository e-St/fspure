namespace FSharp.PureAnalyzer

open System
open System.IO
open FSharp.Analyzers.SDK
open FSharp.Compiler.CodeAnalysis
open FSharp.PureSchema

/// Discovers embedded pure.json manifests in referenced assemblies and composes
/// them with the foundational PureSet (library embeds sit on top of foundational).
///
/// Precedence (Phase 2): foundational base, then library embeds on top.
/// AdditionalFiles / overrides are Phase 6.
module PureManifestLoader =

    type LoadResult =
        {
            Index: PureSet.Index
            /// Assemblies that were successfully opened for PE reading.
            AssembliesRead: int
            /// Successfully parsed pure.json documents.
            ManifestsLoaded: int
            /// Cache key used (empty when no library manifests).
            CacheKey: string
        }

    /// Read pure manifests from the given assembly paths (skips missing/corrupt PE).
    let readManifests (assemblyPaths: string seq) : AssemblyPureManifests list =
        assemblyPaths
        |> Seq.choose (fun path ->
            match PureResourceReader.tryReadFromPath path with
            | Ok m when not m.Resources.IsEmpty -> Some m
            | _ -> None)
        |> Seq.toList

    let private pureFilesOf (manifests: AssemblyPureManifests list) : PureFile list =
        manifests
        |> List.collect PureResourceReader.parsedFiles

    let private cacheParts (manifests: AssemblyPureManifests list) : (Guid * string seq) list =
        manifests
        |> List.map (fun m ->
            let hashes = m.Resources |> List.map _.ContentHash |> Seq.ofList
            m.Mvid, hashes)

    /// Compose foundational + library-embedded PureFiles with caching.
    let composeFromManifests (manifests: AssemblyPureManifests list) : LoadResult =
        let files = pureFilesOf manifests
        let baseIdx = PureSet.foundationalIndex ()

        if files.IsEmpty then
            {
                Index = baseIdx
                AssembliesRead = manifests.Length
                ManifestsLoaded = 0
                CacheKey = ""
            }
        else
            let key = PureSet.makeCompositionCacheKey (cacheParts manifests)
            let idx = PureSet.getOrComposeCached key baseIdx files

            {
                Index = idx
                AssembliesRead = manifests.Length
                ManifestsLoaded = files.Length
                CacheKey = key
            }

    /// Full pipeline: resolve referenced assembly paths → extract pure.json → compose.
    let loadForAnalysis
        (projectOptions: AnalyzerProjectOptions option)
        (projectResults: FSharpCheckProjectResults option)
        : LoadResult =
        let paths = ReferencedAssemblies.resolvePaths projectOptions projectResults
        let manifests = readManifests paths
        composeFromManifests manifests

    /// Load only from explicit assembly paths (tests / controlled scenarios).
    let loadFromPaths (assemblyPaths: string seq) : LoadResult =
        assemblyPaths
        |> Seq.choose (fun p ->
            if String.IsNullOrWhiteSpace p then None
            elif not (File.Exists p) then None
            else Some(Path.GetFullPath p))
        |> readManifests
        |> composeFromManifests
