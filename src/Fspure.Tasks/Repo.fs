namespace Fspure.Tasks

open System
open System.Diagnostics
open System.IO
open System.Text

/// Shared monorepo helpers (root discovery, process runs). No product purity logic.
module Repo =

    let findRootFrom (start: string) : string =
        let rec walk d n =
            if n > 12 then
                failwith $"fspure monorepo root (fspure.slnx) not found from {start}"
            elif File.Exists(Path.Combine(d, "fspure.slnx")) then
                d
            else
                match Directory.GetParent d with
                | null -> failwith $"fspure monorepo root (fspure.slnx) not found from {start}"
                | p -> walk p.FullName (n + 1)

        walk (Path.GetFullPath start) 0

    let findRoot () : string =
        findRootFrom (Directory.GetCurrentDirectory())

    let envOr (name: string) (fallback: string) =
        match Environment.GetEnvironmentVariable name with
        | null
        | "" -> fallback
        | v -> v

    let configuration () = envOr "CONFIGURATION" "Release"

    type RunResult =
        {
            ExitCode: int
            Stdout: string
            Stderr: string
        }

    let run
        (workDir: string)
        (fileName: string)
        (args: string)
        (capture: bool)
        : RunResult
        =
        let psi =
            ProcessStartInfo(
                FileName = fileName,
                Arguments = args,
                WorkingDirectory = workDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = capture,
                RedirectStandardError = capture
            )

        match Process.Start psi with
        | null -> failwith $"failed to start: {fileName} {args}"
        | p ->
            use _ = p

            if not capture then
                p.WaitForExit()
                { ExitCode = p.ExitCode; Stdout = ""; Stderr = "" }
            else
                let out = StringBuilder()
                let err = StringBuilder()
                p.OutputDataReceived.Add(fun e -> if not (isNull e.Data) then out.AppendLine e.Data |> ignore)
                p.ErrorDataReceived.Add(fun e -> if not (isNull e.Data) then err.AppendLine e.Data |> ignore)
                p.BeginOutputReadLine()
                p.BeginErrorReadLine()
                p.WaitForExit()

                {
                    ExitCode = p.ExitCode
                    Stdout = out.ToString()
                    Stderr = err.ToString()
                }

    let runInherit (workDir: string) (fileName: string) (args: string) : int =
        (run workDir fileName args false).ExitCode

    let requireZero (label: string) (code: int) =
        if code <> 0 then
            failwith $"{label} failed (exit {code})"

    let dotnet (root: string) (args: string) : int =
        runInherit root "dotnet" args

    let dotnetCapture (root: string) (args: string) : RunResult =
        run root "dotnet" args true

    let step (msg: string) =
        printfn ""
        printfn "==> %s" msg

    let ok (msg: string) = printfn "OK: %s" msg

    let die (msg: string) =
        eprintfn "ERROR: %s" msg
        exit 1

    let findFirst (dir: string) (pattern: string) : string option =
        if not (Directory.Exists dir) then
            None
        else
            Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories)
            |> Seq.tryHead

    let ensureDir (path: string) =
        Directory.CreateDirectory path |> ignore

    /// Run another in-repo F# tool project with `dotnet run`.
    let runProject (root: string) (projectRel: string) (toolArgs: string list) : int =
        let cfg = configuration ()
        let joined =
            toolArgs
            |> List.map (fun a -> if a.Contains(' ') then $"\"{a}\"" else a)
            |> String.concat " "

        // Put configuration before --project; only args after `--` reach the app.
        let args =
            if joined = "" then
                sprintf "run -c %s --project %s" cfg projectRel
            else
                sprintf "run -c %s --project %s -- %s" cfg projectRel joined

        dotnet root args
