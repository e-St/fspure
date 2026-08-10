namespace Fspure.BuildTasks

open System
open System.IO
open Microsoft.Build.Framework
open Microsoft.Build.Utilities
open Mono.Cecil

/// Injects (or replaces) an embedded pure.json resource into an already-built managed assembly.
/// Used after fspure-collector runs against $(TargetPath).
type EmbedPureJson() =
    inherit Task()

    /// Path to the built assembly (DLL/EXE) to mutate in place.
    [<Required>]
    member val AssemblyPath = "" with get, set

    /// Path to the .pure.json file to embed.
    [<Required>]
    member val PureJsonPath = "" with get, set

    /// Manifest resource name. Convention: {AssemblyName}.pure.json.
    [<Required>]
    member val ResourceName = "" with get, set

    static member private TryAddPackageLibs(resolver: DefaultAssemblyResolver, nugetRoot: string, packageId: string) =
        try
            let pkg = Path.Combine(nugetRoot, packageId)

            if Directory.Exists pkg then
                for verDir in Directory.GetDirectories pkg |> Array.sortDescending do
                    let lib = Path.Combine(verDir, "lib")

                    if Directory.Exists lib then
                        for tfm in Directory.GetDirectories lib do
                            resolver.AddSearchDirectory tfm
        with _ ->
            ()

    static member private AddCommonSearchPaths(resolver: DefaultAssemblyResolver) =
        let nuget =
            match Environment.GetEnvironmentVariable "NUGET_PACKAGES" with
            | null
            | "" ->
                Path.Combine(
                    Environment.GetFolderPath Environment.SpecialFolder.UserProfile,
                    ".nuget",
                    "packages"
                )
            | p -> p

        EmbedPureJson.TryAddPackageLibs(resolver, nuget, "fsharp.core")

        let dotnetRoot =
            match Environment.GetEnvironmentVariable "DOTNET_ROOT" with
            | null
            | "" ->
                match Path.GetDirectoryName(Environment.ProcessPath) with
                | null
                | "" -> "/usr/share/dotnet"
                | d -> d
            | p -> p

        let shared = Path.Combine(dotnetRoot, "shared", "Microsoft.NETCore.App")

        if Directory.Exists shared then
            match Directory.GetDirectories shared |> Array.sortDescending |> Array.tryHead with
            | Some ver -> resolver.AddSearchDirectory ver
            | None -> ()

    override this.Execute() =
        try
            if String.IsNullOrWhiteSpace this.AssemblyPath || not (File.Exists this.AssemblyPath) then
                this.Log.LogError("Fspure EmbedPureJson: assembly not found: {0}", this.AssemblyPath)
                false
            elif String.IsNullOrWhiteSpace this.PureJsonPath || not (File.Exists this.PureJsonPath) then
                this.Log.LogError("Fspure EmbedPureJson: pure.json not found: {0}", this.PureJsonPath)
                false
            elif String.IsNullOrWhiteSpace this.ResourceName then
                this.Log.LogError("Fspure EmbedPureJson: ResourceName is required.")
                false
            else
                let jsonBytes = File.ReadAllBytes this.PureJsonPath
                let assemblyFull = Path.GetFullPath this.AssemblyPath

                let directory =
                    match Path.GetDirectoryName assemblyFull with
                    | null
                    | "" -> "."
                    | d -> d

                use resolver = new DefaultAssemblyResolver()
                resolver.AddSearchDirectory directory
                EmbedPureJson.AddCommonSearchPaths resolver

                let readerParameters =
                    ReaderParameters(
                        AssemblyResolver = resolver,
                        ReadWrite = false,
                        InMemory = true,
                        ReadingMode = ReadingMode.Immediate
                    )

                use moduleDef = ModuleDefinition.ReadModule(assemblyFull, readerParameters)

                for i = moduleDef.Resources.Count - 1 downto 0 do
                    if
                        String.Equals(moduleDef.Resources[i].Name, this.ResourceName, StringComparison.Ordinal)
                    then
                        moduleDef.Resources.RemoveAt i

                moduleDef.Resources.Add(
                    EmbeddedResource(this.ResourceName, ManifestResourceAttributes.Public, jsonBytes)
                )

                let tempPath = assemblyFull + ".fspure-tmp"

                try
                    moduleDef.Write tempPath
                    File.Copy(tempPath, assemblyFull, true)
                finally
                    if File.Exists tempPath then
                        try
                            File.Delete tempPath
                        with _ ->
                            ()

                this.Log.LogMessage(
                    MessageImportance.Low,
                    "Fspure: embedded resource '{0}' ({1} bytes) into {2}",
                    this.ResourceName,
                    jsonBytes.Length,
                    assemblyFull
                )

                true
        with ex ->
            this.Log.LogError("Fspure EmbedPureJson failed: {0}", ex.Message)
            this.Log.LogMessage(MessageImportance.Low, ex.ToString())
            false
