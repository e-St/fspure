module FSharp.PureAnalyzer.Tests.PathResolutionTests

open System
open System.IO
open FSharp.PureAnalyzer
open FSharp.PureSchema
open FSharp.PureAnalyzer.Tests.TestProcess
open Xunit

module private Paths =
    let integration () =
        Path.Combine(repoRoot (), "FSharp.PureAnalyzer.Tests", "integration")

    let pureLibProj () =
        Path.Combine(integration (), "PureLib", "PureLib.fsproj")

    let pureLibDll () =
        Path.Combine(integration (), "PureLib", "bin", "Release", "net10.0", "PureLib.dll")

[<Fact>]
let ``parseReferenceFlags extracts -r and --reference paths that exist`` () =
    let tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dll")
    File.WriteAllBytes(tmp, [| 0uy |])

    try
        let opts =
            [
                "-r:" + tmp
                "--reference:" + tmp
                "/r:" + tmp
                "-g"
                "--optimize+"
            ]

        let paths = ReferencedAssemblies.parseReferenceFlags opts
        Assert.Equal(1, paths.Length)
        Assert.Equal(Path.GetFullPath tmp, paths.Head)
    finally
        if File.Exists tmp then
            File.Delete tmp

[<Fact>]
let ``parseReferenceFlags skips missing files`` () =
    let paths =
        ReferencedAssemblies.parseReferenceFlags [ "-r:/nonexistent/does-not-exist-fspure.dll" ]

    Assert.Empty(paths)

[<Fact>]
let ``ProjectReference-style -r path is obtained for PureLib output`` () =
    let proj = Paths.pureLibProj ()
    Assert.True(File.Exists proj, "missing PureLib.fsproj")

    let code, o, e = runDotnet (repoRoot ()) (sprintf "build \"%s\" -c Release --nologo -v q" proj) 120_000
    assertExitZero "build PureLib" code o e

    let dll = Paths.pureLibDll ()
    Assert.True(File.Exists dll, dll)

    let resolved = ReferencedAssemblies.parseReferenceFlags [ "-r:" + dll ]
    Assert.Equal(1, resolved.Length)
    Assert.Equal(Path.GetFullPath dll, resolved.Head)

    match PureResourceReader.tryReadFromPath dll with
    | Error err -> Assert.Fail err
    | Ok m ->
        Assert.True(m.Resources.Length >= 1)
        let names =
            PureResourceReader.parsedFiles m
            |> List.collect (fun f -> f.PureMethods |> List.map _.FullName)

        Assert.Contains("Fspure.Phase2.PureLib.Api.libraryPureAdd", names)

[<Fact>]
let ``PackageReference-style path is obtained from local nupkg extract`` () =
    let proj = Paths.pureLibProj ()
    let feed = Path.Combine(Paths.integration (), "local-feed")
    Directory.CreateDirectory feed |> ignore

    let code, o, e =
        runDotnet (repoRoot ()) (sprintf "pack \"%s\" -c Release -o \"%s\" --nologo -v q" proj feed) 120_000

    assertExitZero "pack PureLib" code o e

    let gpf = Path.Combine(Path.GetTempPath(), "fspure-phase2-gpf-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory gpf |> ignore

    try
        let consumerProj =
            Path.Combine(Paths.integration (), "ConsumerPackageRef", "ConsumerPackageRef.fsproj")

        let restoreArgs =
            sprintf
                "restore \"%s\" --packages \"%s\" --source \"%s\" --source https://api.nuget.org/v3/index.json -v q"
                consumerProj
                gpf
                feed

        let code2, o2, e2 = runDotnet (repoRoot ()) restoreArgs 180_000
        assertExitZero "restore ConsumerPackageRef" code2 o2 e2

        let dlls =
            Directory.GetFiles(gpf, "PureLib.dll", SearchOption.AllDirectories)
            |> Array.filter (fun p ->
                p.IndexOf(sprintf "%clib%c" Path.DirectorySeparatorChar Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                >= 0)

        Assert.True(dlls.Length >= 1, sprintf "PureLib.dll not found under package cache %s" gpf)

        let dll = dlls.[0]
        let resolved = ReferencedAssemblies.parseReferenceFlags [ "-r:" + dll ]
        Assert.Equal(1, resolved.Length)
        Assert.Equal(Path.GetFullPath dll, resolved.Head)

        match PureResourceReader.tryReadFromPath dll with
        | Error err -> Assert.Fail err
        | Ok m ->
            let names =
                PureResourceReader.parsedFiles m
                |> List.collect (fun f -> f.PureMethods |> List.map _.FullName)

            Assert.Contains("Fspure.Phase2.PureLib.Api.libraryPureAdd", names)
    finally
        try
            Directory.Delete(gpf, true)
        with _ ->
            ()

[<Fact>]
let ``referenced project probe finds PureLib.dll under bin/Release/net10.0`` () =
    let proj = Paths.pureLibProj ()
    let code, o, e = runDotnet (repoRoot ()) (sprintf "build \"%s\" -c Release --nologo -v q" proj) 120_000
    assertExitZero "build PureLib for probe" code o e

    let found = ReferencedAssemblies.fromReferencedProjectPaths [ proj ]
    Assert.True(
        found
        |> List.exists (fun p -> p.EndsWith("PureLib.dll", StringComparison.OrdinalIgnoreCase)),
        sprintf "probe miss: %A" found
    )
