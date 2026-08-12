module fspure.Tests.AnalyzeCliTests

open System
open System.IO
open System.Text
open Fspure.Cli
open Xunit

let private writeSarif (dir: string) =
    let path = Path.Combine(dir, "in.sarif")

    let json =
        """
{"version":"2.1.0","runs":[{"results":[
  {"ruleId":"PURE001","message":{"text":"Call to 'App.Log.write' inside 'App.Core.compute' is not known to be pure."},
   "locations":[{"physicalLocation":{"artifactLocation":{"uri":"src/Core/Logic.fs"},
     "region":{"startLine":12,"startColumn":5,"endLine":12,"endColumn":20}}}]},
  {"ruleId":"PURE002","message":{"text":"Function 'App.Core.compute' is not transitively pure."},
   "locations":[{"physicalLocation":{"artifactLocation":{"uri":"src/Core/Logic.fs"},
     "region":{"startLine":10,"startColumn":5,"endLine":10,"endColumn":12}}}]},
  {"ruleId":"PURE003","message":{"text":"Function 'App.Core.add' is transitively pure."},
   "locations":[{"physicalLocation":{"artifactLocation":{"uri":"src/Core/Math.fs"},
     "region":{"startLine":2,"startColumn":5,"endLine":2,"endColumn":8}}}]},
  {"ruleId":"PURE001","message":{"text":"Call to 'System.Console.WriteLine' inside 'App.Host.main' is not known to be pure."},
   "locations":[{"physicalLocation":{"artifactLocation":{"uri":"src/Host/Program.fs"},
     "region":{"startLine":4,"startColumn":5,"endLine":4,"endColumn":22}}}]},
  {"ruleId":"PURE002","message":{"text":"Function 'App.Host.main' is not transitively pure."},
   "locations":[{"physicalLocation":{"artifactLocation":{"uri":"src/Host/Program.fs"},
     "region":{"startLine":1,"startColumn":5,"endLine":1,"endColumn":9}}}]}
]}]}
"""

    File.WriteAllText(path, json)
    path

[<Fact>]
let ``analyze from sarif with focus and fail-on-impure exits 1`` () =
    let dir = Path.Combine(Path.GetTempPath(), "fspure-analyze-cli", Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore

    try
        let sarif = writeSarif dir

        let opts =
            { AnalyzeOptions.empty with
                SarifInput = Some sarif
                Focus = [ "src/Core" ]
                FailOnImpure = true
                Format = Json
            }

        match Analyze.run opts with
        | Error e -> Assert.Fail e
        | Ok r ->
            Assert.Equal(ExitCode.Impure, r.ExitCode)
            Assert.Equal(1, r.ImpureCount)
            Assert.Equal(1, r.PureCount)
            let calls = r.Diagnostics |> List.filter (fun d -> d.Code = "PURE001")
            Assert.Equal(1, calls.Length)
            Assert.Equal("App.Core.compute", calls[0].Caller)
            Assert.Equal("App.Log.write", calls[0].Callee)
    finally
        if Directory.Exists dir then
            Directory.Delete(dir, true)

[<Fact>]
let ``analyze from sarif without fail-on-impure exits 0`` () =
    let dir = Path.Combine(Path.GetTempPath(), "fspure-analyze-cli", Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore

    try
        let sarif = writeSarif dir

        let opts =
            { AnalyzeOptions.empty with
                SarifInput = Some sarif
                Focus = [ "src/Core" ]
                FailOnImpure = false
            }

        match Analyze.run opts with
        | Error e -> Assert.Fail e
        | Ok r -> Assert.Equal(ExitCode.Success, r.ExitCode)
    finally
        if Directory.Exists dir then
            Directory.Delete(dir, true)

[<Fact>]
let ``two runs of the same sarif produce identical json`` () =
    let dir = Path.Combine(Path.GetTempPath(), "fspure-analyze-cli", Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore

    try
        let sarif = writeSarif dir

        let opts =
            { AnalyzeOptions.empty with
                SarifInput = Some sarif
                Focus = [ "src/Core" ]
                Ignore = [ "src/Host" ]
                Format = Json
            }

        match Analyze.run opts, Analyze.run opts with
        | Ok a, Ok b -> Assert.Equal<byte[]>(a.Bytes, b.Bytes)
        | Error e, _
        | _, Error e -> Assert.Fail e
    finally
        if Directory.Exists dir then
            Directory.Delete(dir, true)

[<Fact>]
let ``cache hit returns the same bytes`` () =
    let dir = Path.Combine(Path.GetTempPath(), "fspure-analyze-cli", Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore

    try
        let sarif = writeSarif dir
        let cache = Path.Combine(dir, "cache")
        let proj = Path.Combine(dir, "Dummy.fsproj")
        File.WriteAllText(proj, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>\n")
        File.WriteAllText(Path.Combine(dir, "Dummy.fs"), "module Dummy\n")

        let opts =
            { AnalyzeOptions.empty with
                Project = Some proj
                SarifInput = Some sarif
                CacheDir = Some cache
                Format = Json
            }

        // --sarif bypasses the report cache (input is already a report). Still must succeed.
        match Analyze.run opts with
        | Error e -> Assert.Fail e
        | Ok r ->
            Assert.False(r.CacheHit)
            Assert.True(r.Bytes.Length > 0)
            Assert.Contains("impureCalls", Encoding.UTF8.GetString r.Bytes)
    finally
        if Directory.Exists dir then
            Directory.Delete(dir, true)

[<Fact>]
let ``missing project without sarif is a usage error`` () =
    match Analyze.run AnalyzeOptions.empty with
    | Error msg -> Assert.Contains("--project", msg)
    | Ok _ -> Assert.Fail "expected error"
