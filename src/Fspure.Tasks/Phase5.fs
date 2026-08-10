namespace Fspure.Tasks

open System
open System.IO
open System.Text.Json

/// Phase 5 regression net (replaces src/scripts/phase5-regression.sh).
module Phase5 =

    let private assertContains (body: string) (needle: string) (label: string) =
        if body.Contains(needle, StringComparison.Ordinal) then
            Repo.ok label
        else
            let lines = body.Split('\n')
            let tail = lines |> Array.skip (max 0 (lines.Length - 40)) |> String.concat "\n"
            eprintfn "%s" tail
            Repo.die $"{label} — missing: {needle}"

    let private runAnalyzers
        (sample: string)
        (root: string)
        (cfg: string)
        (proj: string)
        (analyzerDrop: string)
        (report: string)
        (stdoutPath: string)
        : string
        =
        let toolArgs =
            $"--project \"{proj}\" --analyzers-path \"{analyzerDrop}\" --configuration {cfg} --verbosity normal --report \"{report}\""

        let result =
            if File.Exists(Path.Combine(sample, "dotnet-tools.json")) then
                Repo.runInherit sample "dotnet" "tool restore" |> ignore
                Repo.run sample "dotnet" $"tool run fsharp-analyzers -- {toolArgs}" true
            else
                Repo.runInherit root "dotnet" "tool restore" |> ignore
                Repo.run root "dotnet" $"tool run fsharp-analyzers -- {toolArgs}" true

        let body = result.Stdout + result.Stderr
        File.WriteAllText(stdoutPath, body)

        if File.Exists report then
            body + File.ReadAllText report
        else
            body

    let private checkPureJson (sample: string) =
        let objDir = Path.Combine(sample, "src", "Fspure.ReadyLib", "obj")

        let matches =
            if Directory.Exists objDir then
                Directory.EnumerateFiles(objDir, "Fspure.ReadyLib.pure.json", SearchOption.AllDirectories)
                |> Seq.toList
            else
                []

        match matches with
        | [] -> Repo.die "pure.json not found under obj/"
        | path :: _ ->
            use doc = JsonDocument.Parse(File.ReadAllText path)
            let names =
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

            if names.Contains "Fspure.ReadyLib.Api.impureLog" then
                Repo.die "impureLog must not be in pure.json"

            if not (names.Contains "Fspure.ReadyLib.Api.add") then
                Repo.die "Api.add missing from pure.json"

            printfn "OK: impureLog not in pure.json; Api.add present"
            path

    let run (root: string) : int =
        let cfg = Repo.configuration ()
        let version = Repo.envOr "GATE_VERSION" "0.0.0-ci"
        let sample = Path.Combine(root, "src", "samples", "fspure-ready-lib")
        let feed = Path.Combine(root, "artifacts", "local-feed")
        let art = Path.Combine(root, "artifacts", "phase5")
        let analyzerDrop = Path.Combine(art, "analyzer-drop", "dotnet", "fs")
        let skipPhase1 = Repo.envOr "SKIP_PHASE1" "0" = "1"
        let skipUnit = Repo.envOr "SKIP_UNIT" "0" = "1"

        Repo.ensureDir art
        Repo.ensureDir analyzerDrop

        // 1/5 foundational
        printfn ""
        printfn "======== 1/5  Foundational only (customer-fixture e2e phase1) ========"

        if skipPhase1 then
            printfn "(skipped SKIP_PHASE1=1)"
        else
            let phase1 = Path.Combine(root, "src", "tests", "e2e", "phase1", "run.sh")

            if File.Exists phase1 then
                Repo.runInherit root "bash" $"\"{phase1}\""
                |> Repo.requireZero "phase1 e2e"
                Repo.ok "foundational badges match expectations.json"
            else
                Repo.die "src/tests/e2e/phase1/run.sh missing"

        // 2/5 ready-lib gate
        printfn ""
        printfn "======== 2/5  ReadyLib PackageReference (local-feed gate + golden) ========"
        ReadyLibGate.run root |> Repo.requireZero "ready-lib gate"

        let gateArt = Path.Combine(root, "artifacts", "fspure-ready-lib-gate")
        let gateDrop = Path.Combine(gateArt, "analyzer-drop", "dotnet", "fs")

        if Directory.Exists gateDrop then
            for f in Directory.EnumerateFiles(gateDrop, "*.dll") do
                let name =
                    Path.GetFileName f
                    |> Option.ofObj
                    |> Option.defaultValue "unknown.dll"

                File.Copy(f, Path.Combine(analyzerDrop, name), true)

        let pureJson =
            Repo.findFirst (Path.Combine(sample, "src", "Fspure.ReadyLib", "obj")) "Fspure.ReadyLib.pure.json"
            |> Option.defaultWith (fun () -> Repo.die "ReadyLib pure.json not found after gate")

        let golden = Path.Combine(sample, "scripts", "assert-golden-pure-methods.sh")

        if File.Exists golden then
            Repo.runInherit root "bash" $"\"{golden}\" \"{pureJson}\""
            |> Repo.requireZero "assert-golden-pure-methods"

        File.Copy(pureJson, Path.Combine(art, "Fspure.ReadyLib.pure.json"), true)
        Repo.ok "PackageReference path + golden pure methods"

        // 3/5 ProjectReference
        printfn ""
        printfn "======== 3/5  ReadyLib ProjectReference (same Consumer + library project) ========"

        if not (File.Exists(Path.Combine(feed, "nuget.config"))) then
            Repo.die "local feed missing — gate should have created it"

        if not (File.Exists(Path.Combine(analyzerDrop, "FSharp.PureAnalyzer.dll"))) then
            Repo.die "analyzer drop missing after gate"

        let libProj = Path.Combine(sample, "src", "Fspure.ReadyLib", "Fspure.ReadyLib.fsproj")
        let nugetCfg = Path.Combine(feed, "nuget.config")

        Repo.dotnet
            root
            $"build \"{libProj}\" -c {cfg} --nologo --configfile \"{nugetCfg}\" /p:Version={version} /p:PackageVersion={version} /p:FspureAnalyzerVersion={version} /p:RestoreForce=true"
        |> Repo.requireZero "build ReadyLib project"

        let libDll =
            Repo.findFirst (Path.Combine(sample, "src", "Fspure.ReadyLib", "bin")) "Fspure.ReadyLib.dll"
            |> Option.defaultWith (fun () -> Repo.die "ReadyLib.dll missing after ProjectReference-style build")

        let assertEmbed = Path.Combine(sample, "tests", "AssertEmbed", "AssertEmbed.fsproj")

        Repo.dotnet
            root
            (sprintf
                "run --project \"%s\" -c %s -- \"%s\" Fspure.ReadyLib.Api.add Fspure.ReadyLib.Api.manualEscapeHatch"
                assertEmbed
                cfg
                libDll)
        |> Repo.requireZero "AssertEmbed project build"

        checkPureJson sample |> ignore
        Repo.ok "Project-built ReadyLib embeds pure surface (not impureLog)"

        let reportLib = Path.Combine(art, "readylib-project.sarif")
        let stdoutLib = Path.Combine(art, "readylib-project-stdout.txt")

        let bodyLib =
            runAnalyzers
                sample
                root
                cfg
                libProj
                (Path.Combine(art, "analyzer-drop"))
                reportLib
                stdoutLib

        assertContains
            bodyLib
            "Function 'Fspure.ReadyLib.Api.add' is transitively pure."
            "ReadyLib project: Api.add PURE003"

        assertContains
            bodyLib
            "Function 'Fspure.ReadyLib.Api.impureLog' is not transitively pure."
            "ReadyLib project: Api.impureLog PURE002"

        let consumer = Path.Combine(sample, "tests", "Consumer", "Consumer.fsproj")

        Repo.dotnet
            root
            $"restore \"{consumer}\" --configfile \"{nugetCfg}\" /p:FspureReadyLibUseProjectReference=true /p:FspureAnalyzerVersion={version} /p:RestoreForce=true"
        |> Repo.requireZero "restore consumer ProjectReference"

        Repo.dotnet
            root
            $"build \"{consumer}\" -c {cfg} --nologo --configfile \"{nugetCfg}\" /p:FspureReadyLibUseProjectReference=true /p:FspureAnalyzerVersion={version} --no-restore"
        |> Repo.requireZero "build consumer ProjectReference"

        let reportPr = Path.Combine(art, "consumer-projectref.sarif")
        let stdoutPr = Path.Combine(art, "consumer-projectref-stdout.txt")

        let body =
            runAnalyzers
                sample
                root
                cfg
                consumer
                (Path.Combine(art, "analyzer-drop"))
                reportPr
                stdoutPr

        assertContains body "Function 'Consumer.useAdd' is transitively pure." "Consumer ProjectReference: useAdd PURE003"
        assertContains
            body
            "Function 'Consumer.useFoundational' is transitively pure."
            "Consumer ProjectReference: useFoundational PURE003"

        Repo.ok "ProjectReference path"

        // 4/5 unit tests
        printfn ""
        printfn "======== 4/5  Missing / zero / corrupt pure.json (unit tests) ========"

        if skipUnit then
            printfn "(skipped SKIP_UNIT=1)"
        else
            let home = Environment.GetFolderPath Environment.SpecialFolder.UserProfile
            let paket = Path.Combine(home, ".dotnet", "tools", "paket")
            let anDir = Path.Combine(root, "src", "FSharp.PureAnalyzer")

            if File.Exists(Path.Combine(anDir, "paket.dependencies")) then
                if File.Exists paket then
                    Repo.runInherit anDir paket "restore" |> ignore
                else
                    try
                        Repo.runInherit anDir "paket" "restore" |> ignore
                    with _ ->
                        ()

            Repo.dotnet
                root
                $"test src/tests/FSharp.PureSchema.Tests/FSharp.PureSchema.Tests.fsproj -c {cfg} --verbosity minimal --filter FullyQualifiedName~ResourceReaderTests"
            |> Repo.requireZero "PureSchema ResourceReaderTests"

            let filter =
                "FullyQualifiedName~ManifestIntegrationTests|FullyQualifiedName~CompositionTests|FullyQualifiedName~OverrideTests"

            Repo.dotnet
                root
                (sprintf
                    "test src/tests/FSharp.PureAnalyzer.Tests/FSharp.PureAnalyzer.Tests.fsproj -c %s --verbosity minimal --filter \"%s\""
                    cfg
                    filter)
            |> Repo.requireZero "analyzer unit filters"

            Repo.ok "fallback + composition + override unit tests"

        // 5/5 decoration
        printfn ""
        printfn "======== 5/5  VS Code decoration contract (minimal, no IDE) ========"
        let dec = Path.Combine(root, "src", "Fspure.DecorationLogic.Tests", "Fspure.DecorationLogic.Tests.fsproj")

        if File.Exists dec then
            Repo.dotnet root $"test \"{dec}\" -c Release --nologo -v q"
            |> Repo.requireZero "DecorationLogic.Tests"

            Repo.ok "decoration PURE002/PURE003 → pure/impure labels (F#)"
        else
            printfn "(skipped: DecorationLogic tests project missing)"

        printfn ""
        printfn "✅ Phase 5 regression green"
        printfn "   artifacts: %s" art
        0
