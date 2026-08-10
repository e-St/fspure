namespace Fspure.Tasks

open System
open System.IO
open System.IO.Compression
open System.Text.Json

/// ReadyLib golden / nupkg embed checks (replaces sample bash assert scripts).
module Asserts =

    let private sampleRoot (repoRoot: string) =
        Path.Combine(repoRoot, "src", "samples", "fspure-ready-lib")

    let assertGoldenPureMethods (repoRoot: string) (srcPath: string) : int =
        let sample = sampleRoot repoRoot
        let golden = Path.Combine(sample, "tests", "golden", "Fspure.ReadyLib.pure-methods.golden.txt")

        if not (File.Exists srcPath) then
            Repo.die $"usage: assert-golden <path-to.pure.json|dll> — missing {srcPath}"

        if not (File.Exists golden) then
            Repo.die $"golden missing: {golden}"

        let purePath =
            if srcPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) then
                let beside = Path.Combine(Path.GetDirectoryName srcPath |> Option.ofObj |> Option.defaultValue ".", "Fspure.ReadyLib.pure.json")

                if File.Exists beside then
                    beside
                else
                    Repo.findFirst (Path.Combine(sample, "src", "Fspure.ReadyLib", "obj")) "Fspure.ReadyLib.pure.json"
                    |> Option.defaultWith (fun () -> Repo.die $"could not find Fspure.ReadyLib.pure.json for {srcPath}")
            else
                srcPath

        use doc = JsonDocument.Parse(File.ReadAllText purePath)

        let methods =
            doc.RootElement.GetProperty("pureMethods").EnumerateArray()
            |> Seq.choose (fun m ->
                match m.TryGetProperty "fullName" with
                | true, v ->
                    match v.GetString() with
                    | null
                    | "" -> None
                    | s -> Some s
                | _ -> None)
            |> Set.ofSeq

        let ready =
            methods
            |> Set.filter (fun n -> n.StartsWith("Fspure.ReadyLib.", StringComparison.Ordinal))

        let expected =
            File.ReadAllLines golden
            |> Array.choose (fun line ->
                let t = line.Trim()

                if t = "" || t.StartsWith("#", StringComparison.Ordinal) then
                    None
                else
                    Some t)
            |> Set.ofArray

        if methods.Contains "Fspure.ReadyLib.Api.impureLog" then
            Repo.die "Api.impureLog must not appear in pure.json"

        let missing = Set.difference expected ready |> Set.toList |> List.sort
        let extra = Set.difference ready expected |> Set.toList |> List.sort

        if not missing.IsEmpty then
            eprintfn "ERROR: golden methods missing from pure.json:"

            for m in missing do
                eprintfn "  - %s" m

            exit 1

        if not extra.IsEmpty then
            eprintfn "ERROR: ReadyLib pure methods not in golden (update golden if intentional):"

            for m in extra do
                eprintfn "  + %s" m

            exit 1

        printfn "OK: golden pure methods match (%d Fspure.ReadyLib.* names)" expected.Count
        printfn "    pure.json: %s" purePath
        printfn "    golden:    %s" golden
        0

    let assertNupkgEmbed (repoRoot: string) (nupkg: string) : int =
        if not (File.Exists nupkg) then
            Repo.die $"usage: assert-nupkg <Fspure.ReadyLib.*.nupkg> — missing {nupkg}"

        let sample = sampleRoot repoRoot
        let tmp =
            Path.Combine(Path.GetTempPath(), "fspure-nupkg-" + Guid.NewGuid().ToString("N"))

        try
            Directory.CreateDirectory tmp |> ignore
            ZipFile.ExtractToDirectory(nupkg, tmp)

            let dll =
                Directory.EnumerateFiles(tmp, "Fspure.ReadyLib.dll", SearchOption.AllDirectories)
                |> Seq.tryHead
                |> Option.defaultWith (fun () -> Repo.die "Fspure.ReadyLib.dll missing from nupkg")

            let assertProj = Path.Combine(sample, "tests", "AssertEmbed", "AssertEmbed.fsproj")
            let cfg = Repo.configuration ()

            Repo.dotnet
                repoRoot
                (sprintf
                    "run --project \"%s\" -c %s -- \"%s\" Fspure.ReadyLib.Api.add Fspure.ReadyLib.Api.manualEscapeHatch"
                    assertProj
                    cfg
                    dll)
            |> Repo.requireZero "AssertEmbed nupkg"

            printfn "✅ nupkg embed OK: %s" nupkg
            0
        finally
            if Directory.Exists tmp then
                Directory.Delete(tmp, true)
