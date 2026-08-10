/// Embed (or replace) a pure.json resource into a built managed assembly.
/// CLI: fspure-embed --assembly PATH --pure-json PATH --resource-name NAME
module Fspure.Embed.Program

open System
open System.IO
open Mono.Cecil

let private die (msg: string) =
    eprintfn "Fspure embed: %s" msg
    1

let private addSearchPaths (resolver: DefaultAssemblyResolver) (assemblyDir: string) =
    resolver.AddSearchDirectory assemblyDir

    let nuget =
        Environment.GetEnvironmentVariable "NUGET_PACKAGES"
        |> Option.ofObj
        |> Option.defaultValue (
            Path.Combine(Environment.GetFolderPath Environment.SpecialFolder.UserProfile, ".nuget", "packages")
        )

    try
        let pkg = Path.Combine(nuget, "fsharp.core")

        if Directory.Exists pkg then
            for verDir in Directory.GetDirectories pkg |> Array.sortDescending do
                let lib = Path.Combine(verDir, "lib")

                if Directory.Exists lib then
                    for tfm in Directory.GetDirectories lib do
                        resolver.AddSearchDirectory tfm
    with _ ->
        ()

    let dotnetRoot =
        Environment.GetEnvironmentVariable "DOTNET_ROOT"
        |> Option.ofObj
        |> Option.orElseWith (fun () ->
            Environment.ProcessPath
            |> Option.ofObj
            |> Option.bind (fun p -> Path.GetDirectoryName p |> Option.ofObj))
        |> Option.defaultValue "/usr/share/dotnet"

    let shared = Path.Combine(dotnetRoot, "shared", "Microsoft.NETCore.App")

    if Directory.Exists shared then
        match Directory.GetDirectories shared |> Array.sortDescending |> Array.tryHead with
        | Some ver -> resolver.AddSearchDirectory ver
        | None -> ()

let private parseArgs (argv: string[]) =
    let mutable assembly = ""
    let mutable pureJson = ""
    let mutable resource = ""
    let rec loop i =
        if i >= argv.Length then
            ()
        else
            match argv[i] with
            | "--assembly" when i + 1 < argv.Length ->
                assembly <- argv[i + 1]
                loop (i + 2)
            | "--pure-json" when i + 1 < argv.Length ->
                pureJson <- argv[i + 1]
                loop (i + 2)
            | "--resource-name" when i + 1 < argv.Length ->
                resource <- argv[i + 1]
                loop (i + 2)
            | other ->
                eprintfn "Unknown arg: %s" other
                exit 2

    loop 0
    assembly, pureJson, resource

let private embed (assemblyPath: string) (pureJsonPath: string) (resourceName: string) : int =
    if String.IsNullOrWhiteSpace assemblyPath || not (File.Exists assemblyPath) then
        die $"assembly not found: {assemblyPath}"
    elif String.IsNullOrWhiteSpace pureJsonPath || not (File.Exists pureJsonPath) then
        die $"pure.json not found: {pureJsonPath}"
    elif String.IsNullOrWhiteSpace resourceName then
        die "resource name is required"
    else
        try
            let jsonBytes = File.ReadAllBytes pureJsonPath
            let assemblyFull = Path.GetFullPath assemblyPath

            let directory =
                Path.GetDirectoryName assemblyFull
                |> Option.ofObj
                |> Option.defaultValue "."

            use resolver = new DefaultAssemblyResolver()
            addSearchPaths resolver directory

            let readerParameters =
                ReaderParameters(
                    AssemblyResolver = resolver,
                    ReadWrite = false,
                    InMemory = true,
                    ReadingMode = ReadingMode.Immediate
                )

            use moduleDef = ModuleDefinition.ReadModule(assemblyFull, readerParameters)

            for i = moduleDef.Resources.Count - 1 downto 0 do
                if String.Equals(moduleDef.Resources[i].Name, resourceName, StringComparison.Ordinal) then
                    moduleDef.Resources.RemoveAt i

            moduleDef.Resources.Add(
                EmbeddedResource(resourceName, ManifestResourceAttributes.Public, jsonBytes)
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

            // stdout only — MSBuild Exec with LogStandardErrorAsError treats stderr as failure
            printfn
                "Fspure: embedded resource '%s' (%d bytes) into %s"
                resourceName
                jsonBytes.Length
                assemblyFull

            0
        with ex ->
            die ex.Message

[<EntryPoint>]
let main argv =
    let assembly, pureJson, resource = parseArgs argv
    embed assembly pureJson resource
