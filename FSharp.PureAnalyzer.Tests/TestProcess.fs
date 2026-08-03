module FSharp.PureAnalyzer.Tests.TestProcess

open System
open System.Diagnostics
open System.IO
open Xunit

let repoRoot () : string =
    let start = AppContext.BaseDirectory |> Path.GetFullPath

    let rec walk (dir: string) depth =
        if depth > 12 then
            failwith $"repo root not found from {start}"
        elif File.Exists(Path.Combine(dir, "fspure.slnx")) then
            dir
        else
            match Directory.GetParent dir with
            | null -> failwith $"repo root not found from {start}"
            | parent -> walk parent.FullName (depth + 1)

    walk start 0

let runDotnet (workingDir: string) (args: string) (timeoutMs: int) : int * string * string =
    let psi =
        ProcessStartInfo(
            FileName = "dotnet",
            Arguments = args,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        )

    match Process.Start psi with
    | null -> failwith "failed to start dotnet"
    | proc ->
        use proc = proc
        let stdout = proc.StandardOutput.ReadToEnd()
        let stderr = proc.StandardError.ReadToEnd()
        proc.WaitForExit(timeoutMs) |> ignore
        proc.ExitCode, stdout, stderr

let assertExitZero (label: string) (code: int) (stdout: string) (stderr: string) =
    // Parenthesize boolean: bare `code = 0` is parsed as a named argument in F#.
    Assert.True((code = 0), sprintf "%s failed (exit %d)\nstdout:\n%s\nstderr:\n%s" label code stdout stderr)
