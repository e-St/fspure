namespace Fspure.Cli

open System
open System.Diagnostics
open System.IO
open System.Text

/// Locate FSharp.PureAnalyzer + fsharp-analyzers and run the CliAnalyzer path.
module Host =

    let private log (verbose: bool) (msg: string) =
        if verbose then
            eprintfn "%s" msg

    let private env (name: string) =
        match Environment.GetEnvironmentVariable name with
        | null
        | "" -> None
        | v -> Some v

    let private existsFile p =
        not (String.IsNullOrWhiteSpace p) && File.Exists p

    let private existsDir p =
        not (String.IsNullOrWhiteSpace p) && Directory.Exists p

    let private analyzerDllIn (dir: string) =
        let a = Path.Combine(dir, "FSharp.PureAnalyzer.dll")
        let b = Path.Combine(dir, "dotnet", "fs", "FSharp.PureAnalyzer.dll")

        if File.Exists a then Some dir
        elif File.Exists b then Some dir
        else None

    let rec private walkParents (start: string) (n: int) =
        if n <= 0 then
            []
        else
            match Directory.GetParent start with
            | null -> [ start ]
            | p -> start :: walkParents p.FullName (n - 1)

    /// Directory to pass as `--analyzers-path` (folder that contains the DLL tree).
    let tryFindAnalyzersPath (explicitPath: string option) : string option =
        match explicitPath with
        | Some p when existsDir p -> analyzerDllIn (Path.GetFullPath p)
        | Some p when existsFile p ->
            match Path.GetDirectoryName(Path.GetFullPath p) with
            | null -> None
            | d -> analyzerDllIn d
        | _ ->
            match env "FSPURE_ANALYZERS_PATH" with
            | Some p when existsDir p -> analyzerDllIn (Path.GetFullPath p)
            | _ ->
                let here = AppContext.BaseDirectory

                let candidates =
                    [
                        here
                        Path.Combine(here, "analyzers")
                        Path.Combine(here, "analyzers", "dotnet", "fs")
                        Path.Combine(here, "..", "analyzers")
                    ]

                let fromParents =
                    walkParents (Directory.GetCurrentDirectory()) 8
                    |> List.collect (fun d ->
                        [
                            Path.Combine(d, "analyzers")
                            Path.Combine(d, "analyzers", "dotnet", "fs")
                            Path.Combine(d, "src", "FSharp.PureAnalyzer", "bin", "Release", "net10.0")
                            Path.Combine(d, "src", "FSharp.PureAnalyzer", "bin", "Debug", "net10.0")
                        ])

                let nuget =
                    let home = Environment.GetFolderPath Environment.SpecialFolder.UserProfile
                    let root = Path.Combine(home, ".nuget", "packages", "fsharp.pureanalyzer")

                    if Directory.Exists root then
                        Directory.GetDirectories root
                        |> Array.sort
                        |> Array.rev
                        |> Array.map (fun v -> Path.Combine(v, "analyzers", "dotnet", "fs"))
                        |> Array.toList
                    else
                        []

                (candidates @ fromParents @ nuget)
                |> List.tryPick (fun p ->
                    try
                        if Directory.Exists p then analyzerDllIn (Path.GetFullPath p) else None
                    with _ ->
                        None)

    let private toolExists (path: string) =
        existsFile path || existsFile (path + ".exe")

    let private toolPath (path: string) =
        if existsFile (path + ".exe") then path + ".exe" else path

    let private runCapture (workDir: string) (fileName: string) (args: string) =
        let psi =
            ProcessStartInfo(
                FileName = fileName,
                Arguments = args,
                WorkingDirectory = workDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            )

        match Process.Start psi with
        | null -> Error $"failed to start: {fileName} {args}"
        | p ->
            use _ = p
            let out = StringBuilder()
            let err = StringBuilder()
            p.OutputDataReceived.Add(fun e -> if not (isNull e.Data) then out.AppendLine e.Data |> ignore)
            p.ErrorDataReceived.Add(fun e -> if not (isNull e.Data) then err.AppendLine e.Data |> ignore)
            p.BeginOutputReadLine()
            p.BeginErrorReadLine()
            p.WaitForExit()

            Ok
                {|
                    ExitCode = p.ExitCode
                    Stdout = out.ToString()
                    Stderr = err.ToString()
                |}

    let private tryInstallFsharpAnalyzers (toolDir: string) (verbose: bool) : Result<string, string> =
        Directory.CreateDirectory toolDir |> ignore
        let bin = Path.Combine(toolDir, "fsharp-analyzers")

        if toolExists bin then
            Ok(toolPath bin)
        else
            log verbose $"installing fsharp-analyzers 0.35.0 → {toolDir}"

            match
                runCapture
                    toolDir
                    "dotnet"
                    $"tool install fsharp-analyzers --version 0.35.0 --tool-path \"{toolDir}\""
            with
            | Error e -> Error e
            | Ok r when r.ExitCode <> 0 ->
                Error $"dotnet tool install fsharp-analyzers failed (exit {r.ExitCode}): {r.Stderr}"
            | Ok _ ->
                if toolExists bin then
                    Ok(toolPath bin)
                else
                    Error $"fsharp-analyzers installed but binary missing in {toolDir}"

    let resolveFsharpAnalyzers (explicitPath: string option) (cacheDir: string option) (verbose: bool) : Result<string * string, string> =
        // Returns (exe, extraPrefixArgs) — extraPrefix is "tool run fsharp-analyzers --" via dotnet, or "".
        match explicitPath with
        | Some p when toolExists p -> Ok(toolPath p, "")
        | Some p when existsFile p -> Ok(Path.GetFullPath p, "")
        | _ ->
            match env "FSPURE_FSHARP_ANALYZERS" with
            | Some p when toolExists p -> Ok(toolPath p, "")
            | _ ->
                let cwd = Directory.GetCurrentDirectory()
                let manifest = Path.Combine(cwd, "dotnet-tools.json")
                let parents = walkParents cwd 6

                let hasManifest =
                    parents
                    |> List.exists (fun d ->
                        let m = Path.Combine(d, "dotnet-tools.json")
                        existsFile m && (try File.ReadAllText m with _ -> "").Contains("fsharp-analyzers"))

                if hasManifest || existsFile manifest then
                    Ok("dotnet", "tool run fsharp-analyzers -- ")
                else
                    let toolDir =
                        match cacheDir with
                        | Some d -> Path.Combine(d, "tools")
                        | None -> Path.Combine(Path.GetTempPath(), "fspure-analyze-tools")

                    match tryInstallFsharpAnalyzers toolDir verbose with
                    | Ok exe -> Ok(exe, "")
                    | Error e -> Error e

    let runAnalyzers
        (projectPath: string)
        (analyzersPath: string)
        (configuration: string)
        (sarifOut: string)
        (fsharpExe: string)
        (fsharpPrefix: string)
        (verbose: bool)
        : Result<unit, string> =
        // Run from the caller's cwd so SARIF URIs stay repo-relative (same as phase1).
        let workDir = Directory.GetCurrentDirectory()
        match Path.GetDirectoryName sarifOut with
        | null
        | "" -> ()
        | d -> Directory.CreateDirectory d |> ignore

        let args =
            sprintf
                "%s--project \"%s\" --analyzers-path \"%s\" --configuration %s --verbosity normal --report \"%s\""
                fsharpPrefix
                projectPath
                analyzersPath
                configuration
                sarifOut

        log verbose $"host: {fsharpExe} {args}"

        match runCapture workDir fsharpExe args with
        | Error e -> Error e
        | Ok r ->
            if verbose && r.Stdout <> "" then
                eprintf "%s" r.Stdout

            if verbose && r.Stderr <> "" then
                eprintf "%s" r.Stderr

            if not (File.Exists sarifOut) then
                Error
                    $"fsharp-analyzers did not write SARIF to {sarifOut} (exit {r.ExitCode})\n{r.Stdout}\n{r.Stderr}"
            else
                Ok()
