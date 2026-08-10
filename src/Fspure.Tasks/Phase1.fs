namespace Fspure.Tasks

open System
open System.IO

/// Phase 1 analyzer baseline e2e (replaces src/tests/e2e/phase1/run.sh).
module Phase1 =

    let run (root: string) : int =
        let cfg = Repo.envOr "DOTNET_CONFIGURATION" (Repo.configuration ())
        let fixture = Path.Combine(root, "src", "tests", "e2e", "customer-fixture")
        let analyzerOut = Path.Combine(root, "src", "tests", "e2e", ".artifacts", "analyzer-drop")
        let reportDir = Path.Combine(root, "src", "tests", "e2e", ".artifacts", "phase1")
        let sarif = Path.Combine(reportDir, "customer-fixture.sarif")
        let baseline = Path.Combine(fixture, "expectations.json")
        let dropFs = Path.Combine(analyzerOut, "dotnet", "fs")

        Repo.ensureDir dropFs
        Repo.ensureDir reportDir

        printfn "==> Phase 1: build FSharp.PureAnalyzer (%s)" cfg
        let anDir = Path.Combine(root, "src", "FSharp.PureAnalyzer")

        if File.Exists(Path.Combine(anDir, "paket.dependencies")) then
            let home = Environment.GetFolderPath Environment.SpecialFolder.UserProfile
            let paket = Path.Combine(home, ".dotnet", "tools", "paket")

            if File.Exists paket then
                Repo.runInherit anDir paket "restore" |> ignore
            else
                try
                    Repo.runInherit anDir "paket" "restore" |> ignore
                with _ ->
                    ()

        Repo.dotnet anDir $"build -c {cfg}" |> Repo.requireZero "build analyzer"

        let outDir = Path.Combine(anDir, "bin", cfg, "net10.0")
        let anDll = Path.Combine(outDir, "FSharp.PureAnalyzer.dll")
        let schDll = Path.Combine(outDir, "FSharp.PureSchema.dll")

        if not (File.Exists anDll) then
            Repo.die $"analyzer dll missing at {anDll}"

        if not (File.Exists schDll) then
            Repo.die $"FSharp.PureSchema.dll missing next to analyzer at {outDir}"

        File.Copy(anDll, Path.Combine(dropFs, "FSharp.PureAnalyzer.dll"), true)
        File.Copy(schDll, Path.Combine(dropFs, "FSharp.PureSchema.dll"), true)
        printfn "    DLL → %s" (Path.Combine(dropFs, "FSharp.PureAnalyzer.dll"))

        printfn "==> Phase 1: build customer fixture"
        let fixtureProj = Path.Combine(fixture, "customer-fixture.fsproj")
        Repo.dotnet root $"build \"{fixtureProj}\" -c {cfg}"
        |> Repo.requireZero "build fixture"

        printfn "==> Phase 1: ensure fsharp-analyzers CLI"
        let toolsJson = Path.Combine(root, "dotnet-tools.json")

        let runAnalyzers (args: string) =
            if File.Exists toolsJson && File.ReadAllText(toolsJson).Contains("fsharp-analyzers") then
                Repo.runInherit root "dotnet" "tool restore" |> ignore
                Repo.run root "dotnet" $"tool run fsharp-analyzers -- {args}" true
            else
                let toolDir = Path.Combine(reportDir, "tools")
                Repo.ensureDir toolDir
                let bin = Path.Combine(toolDir, "fsharp-analyzers")

                if not (File.Exists bin) && not (File.Exists(bin + ".exe")) then
                    Repo.dotnet
                        root
                        $"tool install fsharp-analyzers --version 0.35.0 --tool-path \"{toolDir}\""
                    |> Repo.requireZero "install fsharp-analyzers"

                let exe = if File.Exists(bin + ".exe") then bin + ".exe" else bin
                Repo.run root exe args true

        printfn "==> Phase 1: run PureAnalyzer on Program.fs"
        let toolArgs =
            sprintf
                "--project \"%s\" --analyzers-path \"%s\" --configuration %s --verbosity normal --report \"%s\""
                fixtureProj
                analyzerOut
                cfg
                sarif

        let result = runAnalyzers toolArgs
        File.WriteAllText(Path.Combine(reportDir, "analyzer-stdout.txt"), result.Stdout + result.Stderr)

        if not (File.Exists sarif) then
            Repo.die $"SARIF was not written to {sarif}"

        printfn "==> Phase 1: build AssertDefinitionBadges (F#)"
        let assertProj =
            Path.Combine(root, "src", "tests", "e2e", "phase1", "AssertDefinitionBadges", "AssertDefinitionBadges.fsproj")

        Repo.dotnet root $"build \"{assertProj}\" -c Release --nologo -v q"
        |> Repo.requireZero "build AssertDefinitionBadges"

        if Repo.envOr "UPDATE_BASELINE" "0" = "1" then
            printfn "==> Phase 1: UPDATE_BASELINE=1 — rewriting %s from SARIF" baseline

            Repo.dotnet
                root
                (sprintf
                    "run --project \"%s\" -c Release --no-build -- --sarif \"%s\" --expectations \"%s\" --write-baseline \"%s\""
                    assertProj
                    sarif
                    baseline
                    baseline)
            |> Repo.requireZero "write baseline"

            printfn "    Baseline updated. Review the diff and commit if correct."
            0
        else
            printfn "==> Phase 1: compare against baseline expectations.json"
            let report = Path.Combine(reportDir, "badge-report.txt")

            Repo.dotnet
                root
                (sprintf
                    "run --project \"%s\" -c Release --no-build -- --sarif \"%s\" --expectations \"%s\" --write-report \"%s\""
                    assertProj
                    sarif
                    baseline
                    report)
            |> Repo.requireZero "assert badges"

            let dec =
                Path.Combine(root, "src", "Fspure.DecorationLogic.Tests", "Fspure.DecorationLogic.Tests.fsproj")

            if File.Exists dec then
                printfn "==> Phase 1: decoration logic unit contract (F#)"
                Repo.dotnet root $"test \"{dec}\" -c Release --nologo -v q"
                |> Repo.requireZero "DecorationLogic.Tests"

            printfn ""
            printfn "✅ Phase 1 passed — analyzer badges match baseline."
            printfn "   SARIF:  %s" sarif
            printfn "   Report: %s" report
            0
