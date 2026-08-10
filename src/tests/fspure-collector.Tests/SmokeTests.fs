/// Smoke-level checks that a well-formed .pure.json document matches the
/// frozen PureFile schema (used after the collector tool produces output).
module fspure_collector.Tests.SmokeTests

open System
open System.Diagnostics
open System.IO
open FSharp.PureSchema
open Xunit

[<Fact>]
let ``well-formed pure.json from fixture parses as schema 1.0`` () =
    let json =
        """{
  "schemaVersion": "1.0",
  "packageId": "Smoke.Fixture",
  "packageVersion": "0.0.0",
  "generatedAt": "2026-08-03T00:00:00.0000000+00:00",
  "generator": "fsharp-pure-analyzer/fspure-collector/0.1.0",
  "pureMethods": [
    { "fullName": "System.String.Concat", "origin": "automatic" }
  ]
}"""

    match PureFileIO.parse json with
    | Error e -> Assert.Fail(string e)
    | Ok file ->
        Assert.Equal(SchemaVersion.Current, file.SchemaVersion)
        Assert.Equal(1, file.PureMethods.Length)
        Assert.Equal("System.String.Concat", file.PureMethods.Head.FullName)

[<Fact>]
let ``collector tool binary when present produces well-formed pure.json`` () =
    // Optional smoke: only runs when FSPURE_COLLECTOR_DLL is set by CI after build/pack.
    match Environment.GetEnvironmentVariable("FSPURE_COLLECTOR_DLL") with
    | null
    | "" ->
        // Local `dotnet test` without the env var still passes; CI sets the var after pack/build.
        Assert.True(true)
    | toolPath when not (File.Exists toolPath) ->
        Assert.Fail($"FSPURE_COLLECTOR_DLL points to missing file: {toolPath}")
    | toolPath ->
        let work =
            Path.Combine(Path.GetTempPath(), "fspure-collector-smoke", Guid.NewGuid().ToString("N"))

        Directory.CreateDirectory(work) |> ignore
        let outPath = Path.Combine(work, "smoke.pure.json")

        try
            // Analyze the tool's own assembly as a known-good managed DLL.
            let psi =
                ProcessStartInfo(
                    FileName = "dotnet",
                    Arguments =
                        $"\"{toolPath}\" --defaults false --assembly \"{toolPath}\" -o \"{outPath}\" --package-id Smoke.Test --package-version 0.0.0 --public-only false",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                )

            match Process.Start(psi) with
            | null -> Assert.Fail("failed to start fspure-collector process")
            | proc ->
                use proc = proc
                let stdout = proc.StandardOutput.ReadToEnd()
                let stderr = proc.StandardError.ReadToEnd()
                proc.WaitForExit(120_000) |> ignore

                Assert.True(
                    proc.ExitCode = 0,
                    $"collector exited {proc.ExitCode}\nstdout:\n{stdout}\nstderr:\n{stderr}"
                )

                Assert.True(File.Exists outPath, $"expected output at {outPath}")

                match PureFileIO.load outPath with
                | Error e -> Assert.Fail($"output is not a valid PureFile: {e}")
                | Ok file ->
                    Assert.Equal(SchemaVersion.Current, file.SchemaVersion)
                    Assert.Equal("Smoke.Test", file.PackageId)
                    Assert.True(file.PureMethods.Length >= 0)
        finally
            if Directory.Exists work then
                Directory.Delete(work, true)
