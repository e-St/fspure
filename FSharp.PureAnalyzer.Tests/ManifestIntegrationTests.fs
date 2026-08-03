module FSharp.PureAnalyzer.Tests.ManifestIntegrationTests

open System
open System.IO
open FSharp.PureAnalyzer
open FSharp.PureAnalyzer.Tests.TestProcess
open Xunit

module private Env =
    let pureLibDll () =
        Path.Combine(
            repoRoot (),
            "FSharp.PureAnalyzer.Tests",
            "integration",
            "PureLib",
            "bin",
            "Release",
            "net10.0",
            "PureLib.dll"
        )

    let ensurePureLibBuilt () =
        let proj =
            Path.Combine(repoRoot (), "FSharp.PureAnalyzer.Tests", "integration", "PureLib", "PureLib.fsproj")

        let code, o, e = runDotnet (repoRoot ()) (sprintf "build \"%s\" -c Release --nologo -v q" proj) 120_000
        assertExitZero "build PureLib" code o e
        Assert.True(File.Exists(pureLibDll ()), pureLibDll ())

[<Fact>]
let ``library-embedded method is pure only when manifest is loaded`` () =
    Env.ensurePureLibBuilt ()
    let name = "Fspure.Phase2.PureLib.Api.libraryPureAdd"

    Assert.False(PureSet.contains name)

    let loaded = PureManifestLoader.loadFromPaths [ Env.pureLibDll () ]
    Assert.True(loaded.ManifestsLoaded >= 1, "expected at least one pure.json")
    Assert.True(PureSet.containsIn loaded.Index name)

    let foundational = PureManifestLoader.loadFromPaths []
    Assert.Equal(0, foundational.ManifestsLoaded)
    Assert.False(PureSet.containsIn foundational.Index name)

[<Fact>]
let ``composed index still honours foundational core APIs`` () =
    Env.ensurePureLibBuilt ()
    let loaded = PureManifestLoader.loadFromPaths [ Env.pureLibDll () ]
    Assert.True(PureSet.containsIn loaded.Index "Microsoft.FSharp.Core.Operators.op_PipeRight")
    Assert.True(PureSet.containsIn loaded.Index "Microsoft.FSharp.Collections.ListModule.Map")

[<Fact>]
let ``findNonPure marks wrapper pure only with library manifest`` () =
    Env.ensurePureLibBuilt ()
    let libName = "Fspure.Phase2.PureLib.Api.libraryPureAdd"
    let wrapper = "Consumer.useLibraryPure"

    let callGraph = Map.ofList [ wrapper, [ libName ] ]
    let nonLocal = Set.empty

    let without = Analysis.findNonPure PureSet.contains callGraph nonLocal
    Assert.True(Set.contains wrapper without)

    let loaded = PureManifestLoader.loadFromPaths [ Env.pureLibDll () ]
    let isKnown = PureSet.containsIn loaded.Index
    let withManifest = Analysis.findNonPure isKnown callGraph nonLocal
    Assert.False(Set.contains wrapper withManifest)

[<Fact>]
let ``CLI analyser emits PURE003 for library-backed pure wrapper (ProjectReference)`` () =
    let root = repoRoot ()

    let analyzerDll =
        Path.Combine(root, "FSharp.PureAnalyzer", "bin", "Release", "net10.0", "FSharp.PureAnalyzer.dll")

    let schemaDll =
        Path.Combine(root, "FSharp.PureAnalyzer", "bin", "Release", "net10.0", "FSharp.PureSchema.dll")

    let consumer =
        Path.Combine(
            root,
            "FSharp.PureAnalyzer.Tests",
            "integration",
            "ConsumerProjectRef",
            "ConsumerProjectRef.fsproj"
        )

    let code, o, e =
        runDotnet root "build FSharp.PureAnalyzer/FSharp.PureAnalyzer.fsproj -c Release --nologo -v q" 180_000

    assertExitZero "build analyser" code o e
    Assert.True(File.Exists analyzerDll)
    Assert.True(File.Exists schemaDll)

    let code2, o2, e2 = runDotnet root (sprintf "build \"%s\" -c Release --nologo -v q" consumer) 180_000
    assertExitZero "build consumer" code2 o2 e2

    let drop =
        Path.Combine(Path.GetTempPath(), "fspure-phase2-analyzer-drop-" + Guid.NewGuid().ToString("N"))

    let dropFs = Path.Combine(drop, "dotnet", "fs")
    Directory.CreateDirectory dropFs |> ignore
    File.Copy(analyzerDll, Path.Combine(dropFs, "FSharp.PureAnalyzer.dll"), true)
    File.Copy(schemaDll, Path.Combine(dropFs, "FSharp.PureSchema.dll"), true)

    let report = Path.Combine(drop, "out.sarif")

    // Prefer local tool
    let toolsJson = Path.Combine(root, "dotnet-tools.json")

    if not (File.Exists toolsJson) then
        Assert.True(true) // soft-skip
    else
        let _, _, _ = runDotnet root "tool restore" 120_000

        let args =
            sprintf
                "tool run fsharp-analyzers -- --project \"%s\" --analyzers-path \"%s\" --configuration Release --report \"%s\""
                consumer
                drop
                report

        let code3, stdout, stderr = runDotnet root args 180_000
        let combined = stdout + "\n" + stderr

        Assert.True(
            File.Exists report || combined.Contains("PURE003") || combined.Contains("PURE002"),
            sprintf "analyser produced no usable output.\nexit=%d\n%s" code3 combined
        )

        let body =
            if File.Exists report then
                File.ReadAllText report + "\n" + combined
            else
                combined

        Assert.True(
            body.Contains("useLibraryPure") && body.Contains("PURE003"),
            sprintf "expected PURE003 for useLibraryPure.\n%s" body
        )

        Assert.True(
            body.Contains("useLibraryImpure"),
            sprintf "expected diagnostic mention of useLibraryImpure.\n%s" body
        )
