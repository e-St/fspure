namespace FSharp.PureAnalyzer

open System
open System.IO
open FSharp.Analyzers.SDK
open FSharp.Compiler.CodeAnalysis
open FSharp.PureSchema

/// Discovers embedded pure.json manifests in referenced assemblies and composes
/// them with the foundational PureSet, then applies project overrides.
///
/// Precedence (fixed):
///   overrides (add/remove) > library embeds > foundational
///
/// Foundational can be disabled via:
///   - fspure.overrides.json → "useFoundational": false
///   - env FSPURE_DISABLE_FOUNDATIONAL=1|true|yes|on
module PureManifestLoader =

    type LoadResult =
        {
            Index: PureSet.Index
            /// Assemblies that were successfully opened for PE reading.
            AssembliesRead: int
            /// Successfully parsed pure.json documents from libraries.
            ManifestsLoaded: int
            /// Whether foundational set was used as the base.
            UsedFoundational: bool
            /// Path of the overrides file if one was found (even if invalid/ignored).
            OverridesPath: string option
            /// True when a valid overrides document was applied.
            OverridesApplied: bool
            /// Cache key used (empty when nothing to cache).
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

    /// Env FSPURE_DISABLE_FOUNDATIONAL forces foundational off when truthy.
    let envDisablesFoundational () : bool =
        match Environment.GetEnvironmentVariable "FSPURE_DISABLE_FOUNDATIONAL" with
        | null
        | "" -> false
        | v ->
            let t = v.Trim()

            t.Equals("1", StringComparison.OrdinalIgnoreCase)
            || t.Equals("true", StringComparison.OrdinalIgnoreCase)
            || t.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || t.Equals("on", StringComparison.OrdinalIgnoreCase)

    /// Resolve path to fspure.overrides.json if present.
    /// Order: FSPURE_OVERRIDES_PATH → project dir → OtherOptions → cwd.
    let tryFindOverridesPath (projectOptions: AnalyzerProjectOptions option) : string option =
        let fromEnv =
            match Environment.GetEnvironmentVariable "FSPURE_OVERRIDES_PATH" with
            | null
            | "" -> None
            | p when File.Exists p -> Some(Path.GetFullPath p)
            | _ -> None

        match fromEnv with
        | Some p -> Some p
        | None ->
            let candidates = ResizeArray<string>()

            match projectOptions with
            | Some opts ->
                let proj = opts.ProjectFileName

                if not (String.IsNullOrWhiteSpace proj) then
                    match Path.GetDirectoryName proj with
                    | null
                    | "" -> ()
                    | dir -> candidates.Add(Path.Combine(dir, PureOverridesSchema.FileName))

                for o in opts.OtherOptions do
                    if String.IsNullOrWhiteSpace o then
                        ()
                    elif PureOverridesIO.isOverridesFileName o && File.Exists o then
                        candidates.Add o
                    elif o.Contains(PureOverridesSchema.FileName, StringComparison.OrdinalIgnoreCase) then
                        let colon = o.LastIndexOf ':'

                        if colon >= 0 && colon < o.Length - 1 then
                            candidates.Add(o.Substring(colon + 1).Trim())
            | None -> ()

            try
                candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), PureOverridesSchema.FileName))
            with _ ->
                ()

            candidates
            |> Seq.tryPick (fun p ->
                try
                    if String.IsNullOrWhiteSpace p then None
                    elif File.Exists p then Some(Path.GetFullPath p)
                    else None
                with _ ->
                    None)

    let private tryLoadOverrides (pathOpt: string option) : PureOverrides option * string option =
        match pathOpt with
        | None -> None, None
        | Some path ->
            match PureOverridesIO.load path with
            | Ok ov -> Some ov, Some path
            | Error _ -> None, Some path

    /// Compose foundational/empty base + library embeds + optional overrides (cached).
    let composeFull
        (manifests: AssemblyPureManifests list)
        (overrides: PureOverrides option)
        (overridesPath: string option)
        : LoadResult =
        let files = pureFilesOf manifests
        let envOff = envDisablesFoundational ()

        let useFoundational =
            match overrides with
            | Some ov when not ov.UseFoundational -> false
            | _ when envOff -> false
            | _ -> true

        let baseIdx =
            if useFoundational then
                PureSet.foundationalIndex ()
            else
                PureSet.emptyIndex ()

        let libKeyPart =
            if files.IsEmpty then
                "nolibs"
            else
                PureSet.makeCompositionCacheKey (cacheParts manifests)

        let ovKeyPart =
            match overrides with
            | None -> "noov"
            | Some ov -> "ov:" + ov.ContentHash

        let cacheKey =
            libKeyPart
            + (if useFoundational then "|f1" else "|f0")
            + "|"
            + ovKeyPart

        // Cache library composition, then apply overrides under a full key.
        let withLibs = PureSet.getOrComposeCached (cacheKey + "|libs") baseIdx files

        let finalIndex =
            match overrides with
            | None -> withLibs
            | Some ov ->
                // Factory uses compose(preApplied, []) which returns preApplied unchanged.
                let preApplied = PureSet.applyOverrides withLibs ov
                PureSet.getOrComposeCached (cacheKey + "|full") preApplied []

        {
            Index = finalIndex
            AssembliesRead = manifests.Length
            ManifestsLoaded = files.Length
            UsedFoundational = useFoundational
            OverridesPath = overridesPath
            OverridesApplied = overrides.IsSome
            CacheKey = cacheKey
        }

    /// Compose foundational + library-embedded PureFiles with caching (no overrides).
    let composeFromManifests (manifests: AssemblyPureManifests list) : LoadResult =
        composeFull manifests None None

    /// Full pipeline: resolve refs → extract pure.json → find overrides → compose.
    let loadForAnalysis
        (projectOptions: AnalyzerProjectOptions option)
        (projectResults: FSharpCheckProjectResults option)
        : LoadResult =
        let paths = ReferencedAssemblies.resolvePaths projectOptions projectResults
        let manifests = readManifests paths
        let ovPath = tryFindOverridesPath projectOptions
        let ov, pathOut = tryLoadOverrides ovPath
        composeFull manifests ov pathOut

    /// Load only from explicit assembly paths (tests / controlled scenarios).
    let loadFromPaths (assemblyPaths: string seq) : LoadResult =
        assemblyPaths
        |> Seq.choose (fun p ->
            if String.IsNullOrWhiteSpace p then None
            elif not (File.Exists p) then None
            else Some(Path.GetFullPath p))
        |> readManifests
        |> composeFromManifests

    /// Test helper: paths + optional overrides document (no file discovery).
    let loadFromPathsWithOverrides
        (assemblyPaths: string seq)
        (overrides: PureOverrides option)
        : LoadResult =
        let manifests =
            assemblyPaths
            |> Seq.choose (fun p ->
                if String.IsNullOrWhiteSpace p then None
                elif not (File.Exists p) then None
                else Some(Path.GetFullPath p))
            |> readManifests

        composeFull manifests overrides None
