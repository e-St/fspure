module FSharp.PureAnalyzer.Tests.MsBuildEmbeddingTests

open System
open System.IO
open System.IO.Compression
open FSharp.PureAnalyzer
open FSharp.PureAnalyzer.Tests.TestProcess
open FSharp.PureSchema
open Xunit

module private Paths =
    let fixtures () =
        Path.Combine(repoRoot (), "FSharp.PureAnalyzer.Tests", "msbuild-fixtures")

    let embedLibProj () =
        Path.Combine(fixtures (), "EmbedLib", "EmbedLib.fsproj")

    let embedLibDll () =
        Path.Combine(fixtures (), "EmbedLib", "bin", "Release", "net10.0", "EmbedLib.dll")

    let embedConsumerProj () =
        Path.Combine(fixtures (), "EmbedConsumer", "EmbedConsumer.fsproj")

/// Build fspure-collector publish layout + BuildTasks (same as pack prep).
let private ensureToolsBuilt () =
    let root = repoRoot ()
    let tasksProj = Path.Combine(root, "msbuild", "Fspure.BuildTasks", "Fspure.BuildTasks.fsproj")
    let code, o, e = runDotnet root (sprintf "build \"%s\" -c Release --nologo -v q" tasksProj) 120_000
    assertExitZero "build Fspure.BuildTasks" code o e

    let collectorProj = Path.Combine(root, "fspure-collector", "fspure-collector.fsproj")
    let publishOut = Path.Combine(root, "artifacts", "fspure-collector-publish")
    Directory.CreateDirectory publishOut |> ignore

    let code2, o2, e2 =
        runDotnet
            root
            (sprintf
                "publish \"%s\" -c Release -f net10.0 -o \"%s\" --nologo -v q"
                collectorProj
                publishOut)
            180_000

    assertExitZero "publish fspure-collector" code2 o2 e2
    Assert.True(File.Exists(Path.Combine(publishOut, "fspure-collector.dll")))

[<Fact>]
let ``MSBuild targets embed AssemblyName.pure.json into library DLL`` () =
    ensureToolsBuilt ()
    let proj = Paths.embedLibProj ()
    Assert.True(File.Exists proj)

    let code, o, e =
        runDotnet (repoRoot ()) (sprintf "build \"%s\" -c Release --nologo" proj) 180_000

    assertExitZero "build EmbedLib with Fspure targets" code o e

    let dll = Paths.embedLibDll ()
    Assert.True(File.Exists dll, dll)

    match PureResourceReader.tryReadFromPath dll with
    | Error err -> Assert.Fail(sprintf "resource read failed: %s" err)
    | Ok manifests ->
        Assert.Equal("EmbedLib", manifests.AssemblyName)
        Assert.True(manifests.Resources.Length >= 1, "expected embedded pure.json")

        let resourceNames = manifests.Resources |> List.map _.ResourceName
        Assert.Contains("EmbedLib.pure.json", resourceNames)

        let methodNames =
            PureResourceReader.parsedFiles manifests
            |> List.collect (fun f -> f.PureMethods |> List.map _.FullName)

        // Collected pure surface from the library itself
        Assert.Contains("Fspure.Phase3.EmbedLib.Api.embedPureAdd", methodNames)
        // From pure-extra.json merge
        Assert.Contains("Fspure.Phase3.EmbedLib.Api.AuthorClaimedPure", methodNames)

        // Impure must not be claimed pure by collector heuristics
        Assert.DoesNotContain("Fspure.Phase3.EmbedLib.Api.embedImpureLog", methodNames)

[<Fact>]
let ``packed library nupkg contains pure.json resource in the lib DLL`` () =
    ensureToolsBuilt ()
    let proj = Paths.embedLibProj ()
    let outDir =
        Path.Combine(Path.GetTempPath(), "fspure-phase3-pack-" + Guid.NewGuid().ToString("N"))

    Directory.CreateDirectory outDir |> ignore

    try
        let code, o, e =
            runDotnet
                (repoRoot ())
                (sprintf "pack \"%s\" -c Release -o \"%s\" --nologo" proj outDir)
                180_000

        assertExitZero "pack EmbedLib" code o e

        let nupkgs = Directory.GetFiles(outDir, "*.nupkg")
        Assert.True(nupkgs.Length >= 1, "expected nupkg")

        let nupkg = nupkgs.[0]
        let extractDir = Path.Combine(outDir, "extract")
        ZipFile.ExtractToDirectory(nupkg, extractDir)

        let dlls =
            Directory.GetFiles(extractDir, "EmbedLib.dll", SearchOption.AllDirectories)

        Assert.True(dlls.Length >= 1, "EmbedLib.dll missing from nupkg")

        match PureResourceReader.tryReadFromPath dlls.[0] with
        | Error err -> Assert.Fail err
        | Ok m ->
            Assert.Contains("EmbedLib.pure.json", m.Resources |> List.map _.ResourceName)
            let names =
                PureResourceReader.parsedFiles m
                |> List.collect (fun f -> f.PureMethods |> List.map _.FullName)

            Assert.Contains("Fspure.Phase3.EmbedLib.Api.embedPureAdd", names)
    finally
        try
            Directory.Delete(outDir, true)
        with _ ->
            ()

[<Fact>]
let ``local library with embedded pure.json yields pure wrapper via analyser index`` () =
    ensureToolsBuilt ()

    let code, o, e =
        runDotnet
            (repoRoot ())
            (sprintf "build \"%s\" -c Release --nologo -v q" (Paths.embedLibProj ()))
            180_000

    assertExitZero "build EmbedLib" code o e

    let dll = Paths.embedLibDll ()
    let libMethod = "Fspure.Phase3.EmbedLib.Api.embedPureAdd"
    let wrapper = "EmbedConsumer.useEmbedPure"

    Assert.False(PureSet.contains libMethod)

    let loaded = PureManifestLoader.loadFromPaths [ dll ]
    Assert.True(loaded.ManifestsLoaded >= 1)
    Assert.True(PureSet.containsIn loaded.Index libMethod)

    let callGraph = Map.ofList [ wrapper, [ libMethod ] ]
    let without = Analysis.findNonPure PureSet.contains callGraph Set.empty
    Assert.True(Set.contains wrapper without)

    let withEmb =
        Analysis.findNonPure (PureSet.containsIn loaded.Index) callGraph Set.empty

    Assert.False(Set.contains wrapper withEmb)

[<Fact>]
let ``FSharp.PureAnalyzer nupkg ships build targets and fspure-collector tools`` () =
    let root = repoRoot ()
    let analyzerProj = Path.Combine(root, "FSharp.PureAnalyzer", "FSharp.PureAnalyzer.fsproj")
    let outDir =
        Path.Combine(Path.GetTempPath(), "fspure-phase3-analyzer-pack-" + Guid.NewGuid().ToString("N"))

    Directory.CreateDirectory outDir |> ignore

    try
        // paket restore + pack (PrepareFspurePackageTools runs)
        let code, o, e =
            runDotnet root (sprintf "pack \"%s\" -c Release -o \"%s\" --nologo" analyzerProj outDir) 300_000

        assertExitZero "pack FSharp.PureAnalyzer" code o e

        let nupkgs = Directory.GetFiles(outDir, "FSharp.PureAnalyzer*.nupkg")
        Assert.True(nupkgs.Length >= 1)

        let extractDir = Path.Combine(outDir, "extract")
        ZipFile.ExtractToDirectory(nupkgs.[0], extractDir)

        let required =
            [
                Path.Combine(extractDir, "build", "FSharp.PureAnalyzer.props")
                Path.Combine(extractDir, "build", "FSharp.PureAnalyzer.targets")
                Path.Combine(extractDir, "build", "Fspure.BuildTasks.dll")
                Path.Combine(extractDir, "tools", "fspure-collector", "fspure-collector.dll")
                Path.Combine(extractDir, "analyzers", "dotnet", "fs", "FSharp.PureAnalyzer.dll")
                Path.Combine(extractDir, "analyzers", "dotnet", "fs", "FSharp.PureSchema.dll")
            ]

        for p in required do
            Assert.True(File.Exists p, sprintf "missing from nupkg: %s" p)
    finally
        try
            Directory.Delete(outDir, true)
        with _ ->
            ()
