namespace Fspure.Tasks

open System
open System.IO
open System.IO.Compression

/// Monorepo ready-lib end-to-end gate (replaces src/scripts/fspure-ready-lib-gate.sh).
module ReadyLibGate =

    let private paketRestore (root: string) (dirRel: string) =
        let dir = Path.Combine(root, dirRel)

        if File.Exists(Path.Combine(dir, "paket.dependencies")) then
            let home = Environment.GetFolderPath Environment.SpecialFolder.UserProfile
            let tool = Path.Combine(home, ".dotnet", "tools", "paket")

            let code =
                if File.Exists tool then
                    Repo.runInherit dir tool "restore"
                else
                    Repo.runInherit dir "paket" "restore"

            Repo.requireZero $"paket restore {dirRel}" code

    let private writeLocalFeedConfig (feed: string) =
        let cfg =
            $"""<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-feed" value="{feed}" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
"""

        File.WriteAllText(Path.Combine(feed, "nuget.config"), cfg)

    let private assertContains (body: string) (needle: string) (label: string) =
        if body.Contains(needle, StringComparison.Ordinal) then
            Repo.ok label
        else
            let tail =
                let lines = body.Split('\n')
                lines |> Array.skip (max 0 (lines.Length - 80)) |> String.concat "\n"

            eprintfn "---- analyzer output (tail) ----\n%s" tail
            Repo.die $"{label} — missing: {needle}"

    let private assertAbsent (body: string) (needle: string) (label: string) =
        if body.Contains(needle, StringComparison.Ordinal) then
            Repo.die $"{label} — must not appear: {needle}"
        else
            Repo.ok label

    let private extractNupkg (nupkg: string) (dest: string) =
        if Directory.Exists dest then
            Directory.Delete(dest, true)

        Directory.CreateDirectory dest |> ignore
        ZipFile.ExtractToDirectory(nupkg, dest)

    let private runAnalyzers
        (root: string)
        (sample: string)
        (art: string)
        (cfg: string)
        (consumerProj: string)
        (analyzerDrop: string)
        (report: string)
        (stdoutPath: string)
        : string * int
        =
        let toolArgs =
            $"--project \"{consumerProj}\" --analyzers-path \"{analyzerDrop}\" --configuration {cfg} --verbosity normal --report \"{report}\""

        let result =
            if File.Exists(Path.Combine(sample, "dotnet-tools.json")) then
                Repo.runInherit sample "dotnet" "tool restore" |> ignore
                Repo.run sample "dotnet" $"tool run fsharp-analyzers -- {toolArgs}" true
            elif
                File.Exists(Path.Combine(root, "dotnet-tools.json"))
                && File.ReadAllText(Path.Combine(root, "dotnet-tools.json")).Contains("fsharp-analyzers")
            then
                Repo.runInherit root "dotnet" "tool restore" |> ignore
                Repo.run root "dotnet" $"tool run fsharp-analyzers -- {toolArgs}" true
            else
                let toolDir = Path.Combine(art, "tools")
                Repo.ensureDir toolDir
                let bin = Path.Combine(toolDir, "fsharp-analyzers")

                if not (File.Exists bin) && not (File.Exists(bin + ".exe")) then
                    Repo.dotnet
                        root
                        $"tool install fsharp-analyzers --version 0.35.0 --tool-path \"{toolDir}\""
                    |> Repo.requireZero "install fsharp-analyzers"

                let exe = if File.Exists(bin + ".exe") then bin + ".exe" else bin
                Repo.run root exe toolArgs true

        let body = result.Stdout + result.Stderr
        File.WriteAllText(stdoutPath, body)

        if File.Exists report then
            body + File.ReadAllText report, result.ExitCode
        else
            body, result.ExitCode

    let run (root: string) : int =
        let cfg = Repo.configuration ()
        let version = Repo.envOr "GATE_VERSION" "0.0.0-ci"
        let sample = Path.Combine(root, "src", "samples", "fspure-ready-lib")
        let feed = Path.Combine(root, "artifacts", "local-feed")
        let art = Path.Combine(root, "artifacts", "fspure-ready-lib-gate")
        let analyzerDrop = Path.Combine(art, "analyzer-drop", "dotnet", "fs")
        let report = Path.Combine(art, "consumer.sarif")
        let stdoutPath = Path.Combine(art, "analyzer-stdout.txt")

        if not (File.Exists(Path.Combine(root, "src", "FSharp.PureAnalyzer", "FSharp.PureAnalyzer.fsproj"))) then
            Repo.die "run from fspure monorepo (FSharp.PureAnalyzer missing)"

        if not (File.Exists(Path.Combine(sample, "src", "Fspure.ReadyLib", "Fspure.ReadyLib.fsproj"))) then
            Repo.die "sample missing: src/samples/fspure-ready-lib"

        Repo.ensureDir feed
        Repo.ensureDir analyzerDrop
        Repo.ensureDir art

        for f in Directory.EnumerateFiles(feed, "*.nupkg") do
            File.Delete f

        for f in Directory.EnumerateFiles(feed, "*.snupkg") do
            File.Delete f

        // Drop stale global packages for this gate version.
        let gpf =
            match Environment.GetEnvironmentVariable "NUGET_PACKAGES" with
            | null
            | "" ->
                Path.Combine(
                    Environment.GetFolderPath Environment.SpecialFolder.UserProfile,
                    ".nuget",
                    "packages"
                )
            | p -> p

        for id in [ "fsharp.pureanalyzer"; "fspure.readylib" ] do
            let d1 = Path.Combine(gpf, id, version.ToLowerInvariant())
            let d2 = Path.Combine(gpf, id, version)

            if Directory.Exists d1 then
                Directory.Delete(d1, true)

            if Directory.Exists d2 then
                Directory.Delete(d2, true)

        writeLocalFeedConfig feed

        Repo.step $"Pack FSharp.PureAnalyzer {version} → {feed}"
        paketRestore root "src/FSharp.PureAnalyzer"
        paketRestore root "src/fspure-collector"

        Repo.dotnet
            root
            $"pack src/FSharp.PureAnalyzer/FSharp.PureAnalyzer.fsproj -c {cfg} -o \"{feed}\" --nologo -v minimal /p:Version={version} /p:PackageVersion={version}"
        |> Repo.requireZero "pack analyzer"

        let analyzerNupkg =
            Directory.EnumerateFiles(feed, $"FSharp.PureAnalyzer.{version}*.nupkg")
            |> Seq.tryHead
            |> Option.defaultWith (fun () -> Repo.die "analyzer nupkg not produced")

        Repo.ok $"analyzer nupkg: {analyzerNupkg}"

        let tmpAn = Path.Combine(Path.GetTempPath(), "fspure-gate-an-" + Guid.NewGuid().ToString("N"))

        try
            extractNupkg analyzerNupkg tmpAn

            if not (File.Exists(Path.Combine(tmpAn, "build", "FSharp.PureAnalyzer.targets"))) then
                Repo.die "packed analyzer missing build/FSharp.PureAnalyzer.targets"

            let hasCollector =
                File.Exists(Path.Combine(tmpAn, "tools", "fspure-collector", "fspure-collector.dll"))
                || File.Exists(Path.Combine(tmpAn, "tools", "purity-collector", "purity-collector.dll"))

            if not hasCollector then
                Repo.die "packed analyzer missing tools/fspure-collector/"

            let anDll = Path.Combine(tmpAn, "analyzers", "dotnet", "fs", "FSharp.PureAnalyzer.dll")
            let schDll = Path.Combine(tmpAn, "analyzers", "dotnet", "fs", "FSharp.PureSchema.dll")

            if not (File.Exists anDll) then
                Repo.die "packed analyzer missing analyzers/dotnet/fs/FSharp.PureAnalyzer.dll"

            if not (File.Exists schDll) then
                Repo.die "packed analyzer missing analyzers/dotnet/fs/FSharp.PureSchema.dll"

            File.Copy(anDll, Path.Combine(analyzerDrop, "FSharp.PureAnalyzer.dll"), true)
            File.Copy(schDll, Path.Combine(analyzerDrop, "FSharp.PureSchema.dll"), true)
            Repo.ok "Phase 3 package layout (build/ + tools/ + analyzers/)"
        finally
            if Directory.Exists tmpAn then
                Directory.Delete(tmpAn, true)

        Repo.step $"Pack Fspure.ReadyLib {version} (embed pure.json)"

        let readyLibProj =
            Path.Combine(sample, "src", "Fspure.ReadyLib", "Fspure.ReadyLib.fsproj")

        let nugetCfg = Path.Combine(feed, "nuget.config")

        Repo.dotnet
            root
            (sprintf
                "pack \"%s\" -c %s -o \"%s\" --nologo -v minimal --configfile \"%s\" /p:Version=%s /p:PackageVersion=%s /p:FspureAnalyzerVersion=%s /p:RestoreForce=true"
                readyLibProj
                cfg
                feed
                nugetCfg
                version
                version
                version)
        |> Repo.requireZero "pack ReadyLib"

        let libNupkg =
            Directory.EnumerateFiles(feed, $"Fspure.ReadyLib.{version}*.nupkg")
            |> Seq.tryHead
            |> Option.defaultWith (fun () -> Repo.die "ReadyLib nupkg not produced")

        Repo.ok $"ReadyLib nupkg: {libNupkg}"

        let dll =
            Repo.findFirst (Path.Combine(sample, "src", "Fspure.ReadyLib", "bin")) "Fspure.ReadyLib.dll"
            |> Option.defaultWith (fun () -> Repo.die "Fspure.ReadyLib.dll not found after pack")

        Repo.step "Assert embedded pure.json (DLL)"

        let assertEmbed = Path.Combine(sample, "tests", "AssertEmbed", "AssertEmbed.fsproj")

        Repo.dotnet
            root
            (sprintf
                "run --project \"%s\" -c %s -- \"%s\" Fspure.ReadyLib.Api.add Fspure.ReadyLib.Api.mul Fspure.ReadyLib.Api.manualEscapeHatch"
                assertEmbed
                cfg
                dll)
        |> Repo.requireZero "AssertEmbed DLL"

        Repo.ok "DLL embed"

        Repo.step "Assert embedded pure.json (nupkg)"
        // Prefer F# AssertEmbed path for nupkg: extract and check DLL if assert-nupkg script exists
        let assertNupkg = Path.Combine(sample, "scripts", "assert-nupkg-embed.sh")

        if File.Exists assertNupkg then
            Repo.runInherit root "bash" $"\"{assertNupkg}\" \"{libNupkg}\""
            |> Repo.requireZero "assert-nupkg-embed"
        else
            // Fallback: extract lib nupkg and AssertEmbed on the library dll inside
            let tmpLib = Path.Combine(Path.GetTempPath(), "fspure-gate-lib-" + Guid.NewGuid().ToString("N"))

            try
                extractNupkg libNupkg tmpLib

                let libDll =
                    Directory.EnumerateFiles(tmpLib, "Fspure.ReadyLib.dll", SearchOption.AllDirectories)
                    |> Seq.tryHead
                    |> Option.defaultWith (fun () -> Repo.die "ReadyLib.dll missing inside nupkg")

                Repo.dotnet
                    root
                    (sprintf
                        "run --project \"%s\" -c %s -- \"%s\" Fspure.ReadyLib.Api.add"
                        assertEmbed
                        cfg
                        libDll)
                |> Repo.requireZero "AssertEmbed nupkg dll"
            finally
                if Directory.Exists tmpLib then
                    Directory.Delete(tmpLib, true)

        Repo.ok "nupkg embed"

        Repo.step "Restore + build consumer from local feed"
        let consumer = Path.Combine(sample, "tests", "Consumer", "Consumer.fsproj")

        Repo.dotnet
            root
            $"restore \"{consumer}\" --configfile \"{nugetCfg}\" /p:FspureReadyLibVersion={version} /p:RestoreForce=true"
        |> Repo.requireZero "restore consumer"

        Repo.dotnet
            root
            $"build \"{consumer}\" -c {cfg} --nologo --configfile \"{nugetCfg}\" /p:FspureReadyLibVersion={version} --no-restore"
        |> Repo.requireZero "build consumer"

        Repo.ok "consumer built"

        Repo.step "Run fsharp-analyzers on consumer"

        let body, analyzerExit =
            runAnalyzers
                root
                sample
                art
                cfg
                consumer
                (Path.Combine(art, "analyzer-drop"))
                report
                stdoutPath

        Repo.step "Hard asserts (library embed must drive pure/impure labels)"
        assertContains body "Function 'Consumer.useAdd' is transitively pure." "useAdd PURE003 (library embed consumed)"
        assertContains body "Function 'Consumer.useImpure' is not transitively pure." "useImpure PURE002"
        assertContains body "Function 'Consumer.useFoundational' is transitively pure." "useFoundational PURE003 (foundational still works)"
        assertContains body "Function 'Consumer.useMap' is transitively pure." "useMap PURE003"
        assertAbsent body "Function 'Consumer.useAdd' is not transitively pure." "useAdd must not be impure"
        assertAbsent body "Function 'Consumer.useImpure' is transitively pure." "useImpure must not be pure"
        assertContains body "PURE002" "code PURE002 present"
        assertContains body "PURE003" "code PURE003 present"

        try
            let anName = Path.GetFileName analyzerNupkg |> Option.ofObj |> Option.defaultValue "analyzer.nupkg"
            let libName = Path.GetFileName libNupkg |> Option.ofObj |> Option.defaultValue "readylib.nupkg"
            File.Copy(analyzerNupkg, Path.Combine(art, anName), true)
            File.Copy(libNupkg, Path.Combine(art, libName), true)
        with _ ->
            ()

        printfn ""
        printfn "✅ fspure-ready-lib gate green"
        printfn "   version:  %s" version
        printfn "   feed:     %s" feed
        printfn "   artifacts:%s" art
        printfn "   analyzer exit (informational): %d" analyzerExit
        0
