namespace Fspure.Tasks

open System
open System.IO
open System.Text.RegularExpressions

/// NuGet vulnerable-package scan + npm audit (replaces src/scripts/security-audit.sh).
module Security =

    let private projects =
        [
            "src/FSharp.PureSchema/FSharp.PureSchema.fsproj"
            "src/tests/FSharp.PureSchema.Tests/FSharp.PureSchema.Tests.fsproj"
            "src/FSharp.PureAnalyzer/FSharp.PureAnalyzer.fsproj"
            "src/tests/FSharp.PureAnalyzer.Tests/FSharp.PureAnalyzer.Tests.fsproj"
            "src/fspure-collector/fspure-collector.fsproj"
            "src/tests/fspure-collector.Tests/fspure-collector.Tests.fsproj"
            "src/Fspure.Embed/Fspure.Embed.fsproj"
            "src/Fspure.DecorationLogic/Fspure.DecorationLogic.fsproj"
            "src/tests/e2e/phase2/ScreenshotCapture/ScreenshotCapture.fsproj"
            "src/DocsGenerator/DocsGenerator.fsproj"
            "src/Fspure.Tasks/Fspure.Tasks.fsproj"
            "src/samples/fspure-ready-lib/src/Fspure.ReadyLib/Fspure.ReadyLib.fsproj"
        ]

    let private vulnerableRe =
        Regex(@"has the following vulnerable packages", RegexOptions.IgnoreCase ||| RegexOptions.Compiled)

    let private advisoryRe =
        Regex(@"GHSA-[0-9a-z-]+|CVE-[0-9]{4}-", RegexOptions.IgnoreCase ||| RegexOptions.Compiled)

    let private tryPaketRestore (root: string) (dirRel: string) =
        let dir = Path.Combine(root, dirRel)
        let deps = Path.Combine(dir, "paket.dependencies")

        if File.Exists deps then
            let home = Environment.GetFolderPath Environment.SpecialFolder.UserProfile
            let tool = Path.Combine(home, ".dotnet", "tools", "paket")

            let code =
                if File.Exists tool then
                    Repo.runInherit dir tool "restore"
                else
                    // try PATH paket
                    try
                        Repo.runInherit dir "paket" "restore"
                    with _ ->
                        0

            if code <> 0 then
                eprintfn "warning: paket restore failed in %s" dirRel

    let private scanNuget (root: string) (projRel: string) (fail: bool ref) =
        let proj = Path.Combine(root, projRel.Replace('/', Path.DirectorySeparatorChar))

        if not (File.Exists proj) then
            printfn "skip missing %s" projRel
        else
            printfn "--> %s" projRel

            let restore1 =
                Repo.dotnetCapture
                    root
                    $"restore \"{proj}\" --source https://api.nuget.org/v3/index.json /p:TreatWarningsAsErrors=false /p:RestoreIgnoreFailedSources=true -v q"

            if restore1.ExitCode <> 0 then
                let restore2 =
                    Repo.dotnetCapture
                        root
                        $"restore \"{proj}\" /p:TreatWarningsAsErrors=false /p:RestoreIgnoreFailedSources=true -v q"

                if restore2.ExitCode <> 0 then
                    eprintfn "%s%s" restore1.Stdout restore1.Stderr
                    eprintfn "ERROR: restore failed: %s" projRel
                    fail.Value <- true

            let listed =
                Repo.dotnetCapture root $"list \"{proj}\" package --vulnerable --include-transitive"

            let body = listed.Stdout + listed.Stderr
            printf "%s" body

            if vulnerableRe.IsMatch body || advisoryRe.IsMatch body then
                eprintfn "ERROR: vulnerable package(s) in %s" projRel
                fail.Value <- true

    let private npmAudit (root: string) (fail: bool ref) =
        printfn ""
        printfn "======== npm audit (vscode-extension) ========"
        let ext = Path.Combine(root, "src", "editor", "vscode-extension")

        if not (File.Exists(Path.Combine(ext, "package.json"))) then
            printfn "skip missing vscode-extension"
        else
            try
                let _ = Repo.runInherit ext "npm" "--version" |> ignore

                if File.Exists(Path.Combine(ext, "package-lock.json")) then
                    let c = Repo.runInherit ext "npm" "ci --ignore-scripts"

                    if c <> 0 then
                        Repo.runInherit ext "npm" "install --ignore-scripts" |> ignore
                else
                    Repo.runInherit ext "npm" "install --ignore-scripts" |> ignore

                let level = Repo.envOr "NPM_AUDIT_LEVEL" "high"
                let code = Repo.runInherit ext "npm" $"audit --audit-level={level}"

                if code <> 0 then
                    eprintfn "ERROR: npm audit failed (level=%s) in vscode-extension" level
                    fail.Value <- true
            with _ ->
                printfn "npm not installed — skip vscode-extension audit"

    let run (root: string) : int =
        let fail = ref false

        printfn "======== NuGet: restore + list --vulnerable ========"
        tryPaketRestore root "src/FSharp.PureAnalyzer"
        tryPaketRestore root "src/fspure-collector"

        for p in projects do
            scanNuget root p fail

        npmAudit root fail

        printfn ""
        printfn "======== Summary ========"

        if fail.Value then
            printfn "Security audit FAILED"
            1
        else
            printfn "Security audit OK (NuGet vulnerable scan + npm audit)"
            0
