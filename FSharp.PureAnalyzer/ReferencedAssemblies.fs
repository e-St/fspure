namespace FSharp.PureAnalyzer

open System
open System.IO
open FSharp.Analyzers.SDK
open FSharp.Compiler.CodeAnalysis

/// Resolves filesystem paths of referenced managed assemblies for pure.json discovery.
///
/// ## Strategy (net10.0, fixed precedence within a call)
///
/// 1. **OtherOptions** (`AnalyzerProjectOptions.OtherOptions`) — richest FCS surface.
///    Parse `-r:`, `/r:`, and `--reference:` entries. These cover both ProjectReference
///    and PackageReference assemblies when the project was type-checked with full options
///    (CLI `fsharp-analyzers` and typical Ionide checks).
///
/// 2. **DependencyFiles** (`FSharpCheckProjectResults.DependencyFiles`) — secondary.
///    Used when OtherOptions is empty/incomplete (some editor partial-check paths).
///    Filter to existing `*.dll` paths.
///
/// 3. **ReferencedProjectsPath** — project file paths for ProjectReferences. Probe each
///    project's conventional net10.0 output folders for `{ProjectName}.dll` /
///    `{AssemblyName}.dll` when the DLL was not already found via (1)/(2).
///
/// 4. **NuGet package cache probe** — last resort for PackageReference identities that
///    appear only as simple names. Looks under `NUGET_PACKAGES` / `~/.nuget/packages`
///    for `*/lib/net10.0/{Name}.dll` (and `net10.0-*`).
///
/// ## Known limitations
///
/// - Multi-targeting is out of scope (everything is net10.0).
/// - ProjectReference output is assumed under `bin/{Debug|Release}/net10.0/`.
/// - PrivateAssets / runtime-only packages may not appear in OtherOptions.
/// - Framework reference assemblies (shared framework) are included if FCS listed them;
///   pure.json extraction is a no-op when none is embedded.
/// - Corrupt/missing paths are skipped (never fail analysis).
module ReferencedAssemblies =

    type ResolutionSource =
        | OtherOptions
        | DependencyFiles
        | ReferencedProjectProbe
        | NuGetCacheProbe

    type ResolvedAssembly =
        {
            Path: string
            Source: ResolutionSource
        }

    let private isDll (path: string) =
        path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)

    let private normalizeExisting (path: string) : string option =
        if String.IsNullOrWhiteSpace path then
            None
        else
            try
                let full = Path.GetFullPath path
                if File.Exists full then Some full else None
            with _ ->
                None

    /// Parse FCS-style reference flags from OtherOptions.
    let parseReferenceFlags (otherOptions: string seq) : string list =
        otherOptions
        |> Seq.choose (fun o ->
            if String.IsNullOrWhiteSpace o then
                None
            elif o.StartsWith("-r:", StringComparison.Ordinal) then
                Some(o.Substring(3))
            elif o.StartsWith("/r:", StringComparison.Ordinal) then
                Some(o.Substring(3))
            elif o.StartsWith("--reference:", StringComparison.OrdinalIgnoreCase) then
                Some(o.Substring("--reference:".Length))
            else
                None)
        |> Seq.choose normalizeExisting
        |> Seq.distinct
        |> Seq.toList

    let fromDependencyFiles (projectResults: FSharpCheckProjectResults) : string list =
        try
            projectResults.DependencyFiles
            |> Array.filter isDll
            |> Array.choose normalizeExisting
            |> Array.distinct
            |> Array.toList
        with _ ->
            []

    let private nugetPackagesRoot () : string =
        match Environment.GetEnvironmentVariable "NUGET_PACKAGES" with
        | null
        | "" -> Path.Combine(Environment.GetFolderPath Environment.SpecialFolder.UserProfile, ".nuget", "packages")
        | p -> p

    /// Probe bin/{Debug,Release}/net10.0* under a project directory for a named assembly.
    let private probeProjectOutput (projectPath: string) : string list =
        try
            let projectDirOpt: string option =
                if File.Exists projectPath then
                    match Path.GetDirectoryName projectPath with
                    | null
                    | "" -> None
                    | d -> Some d
                elif Directory.Exists projectPath then
                    Some projectPath
                else
                    None

            match projectDirOpt with
            | None -> []
            | Some projectDir ->
                match Path.GetFileNameWithoutExtension projectPath with
                | null
                | "" -> []
                | name ->
                    let configs = [| "Release"; "Debug" |]
                    let found = ResizeArray<string>()

                    for cfg in configs do
                        let binRoot = Path.Combine(projectDir, "bin", cfg)

                        if Directory.Exists binRoot then
                            for tfmDir in Directory.EnumerateDirectories(binRoot, "net10.0*") do
                                let candidate = Path.Combine(tfmDir, name + ".dll")

                                match normalizeExisting candidate with
                                | Some p -> found.Add p
                                | None -> ()

                    found |> Seq.distinct |> Seq.toList
        with _ ->
            []

    let fromReferencedProjectPaths (projectPaths: string seq) : string list =
        projectPaths
        |> Seq.collect probeProjectOutput
        |> Seq.distinct
        |> Seq.toList

    /// Last-resort: find net10.0 lib DLL by package/assembly simple name in the NuGet cache.
    let probeNuGetCache (assemblySimpleName: string) : string option =
        if String.IsNullOrWhiteSpace assemblySimpleName then
            None
        else
            try
                let root = nugetPackagesRoot ()
                let pkgId = assemblySimpleName.ToLowerInvariant()
                let pkgDir = Path.Combine(root, pkgId)

                if not (Directory.Exists pkgDir) then
                    None
                else
                    // Prefer highest version folder that has lib/net10.0*/{Name}.dll
                    let versions =
                        Directory.EnumerateDirectories pkgDir
                        |> Seq.choose (fun d ->
                            match Path.GetFileName d with
                            | null
                            | "" -> None
                            | name -> Some name)
                        |> Seq.sort
                        |> Seq.rev
                        |> Seq.toList

                    let rec search remaining =
                        match remaining with
                        | [] -> None
                        | ver :: rest ->
                            let lib = Path.Combine(pkgDir, ver, "lib")

                            if not (Directory.Exists lib) then
                                search rest
                            else
                                let hit =
                                    Directory.EnumerateDirectories(lib, "net10.0*")
                                    |> Seq.tryPick (fun tfm ->
                                        normalizeExisting (Path.Combine(tfm, assemblySimpleName + ".dll")))

                                match hit with
                                | Some p -> Some p
                                | None -> search rest

                    search versions
            with _ ->
                None

    let private addResolved
        (seen: System.Collections.Generic.HashSet<string>)
        (acc: ResizeArray<ResolvedAssembly>)
        (source: ResolutionSource)
        (paths: string list)
        =
        for p in paths do
            let key = p.ToLowerInvariant()

            if seen.Add key then
                acc.Add({ Path = p; Source = source })

    /// Resolve referenced assembly paths using the documented strategy.
    let resolve
        (projectOptions: AnalyzerProjectOptions option)
        (projectResults: FSharpCheckProjectResults option)
        : ResolvedAssembly list =
        let seen = System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
        let acc = ResizeArray<ResolvedAssembly>()

        match projectOptions with
        | Some opts ->
            let fromFlags = parseReferenceFlags opts.OtherOptions
            addResolved seen acc OtherOptions fromFlags

            // Project references not already covered as -r: outputs
            let fromProjects = fromReferencedProjectPaths opts.ReferencedProjectsPath
            addResolved seen acc ReferencedProjectProbe fromProjects
        | None -> ()

        match projectResults with
        | Some pr ->
            let deps = fromDependencyFiles pr
            addResolved seen acc DependencyFiles deps
        | None -> ()

        acc |> Seq.toList

    /// Convenience: paths only, distinct, existing files.
    let resolvePaths
        (projectOptions: AnalyzerProjectOptions option)
        (projectResults: FSharpCheckProjectResults option)
        : string list =
        resolve projectOptions projectResults |> List.map _.Path

    /// Extend a resolved set by probing the NuGet cache for simple names still missing.
    /// Used when tests/tools supply assembly identities without full OtherOptions.
    let supplementFromNuGetCache (simpleNames: string seq) (already: ResolvedAssembly list) : ResolvedAssembly list =
        let seen =
            System.Collections.Generic.HashSet<string>(
                already |> Seq.map (fun a -> a.Path.ToLowerInvariant()),
                StringComparer.OrdinalIgnoreCase
            )

        let acc = ResizeArray<ResolvedAssembly>(already)

        for name in simpleNames do
            match probeNuGetCache name with
            | None -> ()
            | Some p ->
                let key = p.ToLowerInvariant()

                if seen.Add key then
                    acc.Add({ Path = p; Source = NuGetCacheProbe })

        acc |> Seq.toList
