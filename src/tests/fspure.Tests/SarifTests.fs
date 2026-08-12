module fspure.Tests.SarifTests

open System
open System.IO
open Fspure.Cli
open Xunit

let private sampleSarif =
    """
{
  "version": "2.1.0",
  "runs": [
    {
      "results": [
        {
          "ruleId": "PURE001",
          "message": { "text": "Call to 'System.Console.WriteLine' is not known to be pure." },
          "locations": [
            {
              "physicalLocation": {
                "artifactLocation": { "uri": "src/Core/Logic.fs" },
                "region": { "startLine": 4, "startColumn": 5, "endLine": 4, "endColumn": 20 }
              }
            }
          ]
        },
        {
          "ruleId": "PURE002",
          "message": { "text": "Function 'App.Core.compute' is not transitively pure." },
          "locations": [
            {
              "physicalLocation": {
                "artifactLocation": { "uri": "src/Core/Logic.fs" },
                "region": { "startLine": 10, "startColumn": 5, "endLine": 10, "endColumn": 12 }
              }
            }
          ]
        },
        {
          "ruleId": "PURE003",
          "message": { "text": "Function 'App.Core.add' is transitively pure." },
          "locations": [
            {
              "physicalLocation": {
                "artifactLocation": { "uri": "file:///tmp/proj/src/Core/Math.fs" },
                "region": { "startLine": 2, "startColumn": 5, "endLine": 2, "endColumn": 8 }
              }
            }
          ]
        }
      ]
    }
  ]
}
"""

[<Fact>]
let ``parse keeps PURE001 with callee and definition names`` () =
    match SarifRead.parse "/tmp/proj" sampleSarif with
    | Error e -> Assert.Fail e
    | Ok items ->
        Assert.Equal(3, items.Length)
        Assert.Equal("PURE001", items[0].Code)
        Assert.Equal("System.Console.WriteLine", items[0].Callee)
        Assert.Equal("PURE002", items[1].Code)
        Assert.Equal("App.Core.compute", items[1].FullName)
        Assert.Equal("src/Core/Logic.fs", items[1].File)
        Assert.Equal(10, items[1].StartLine)
        Assert.Equal("PURE003", items[2].Code)
        Assert.Equal("App.Core.add", items[2].FullName)
        Assert.True(
            items[2].File = "src/Core/Math.fs" || items[2].File.EndsWith("src/Core/Math.fs"),
            items[2].File
        )

[<Fact>]
let ``relative uri customer-fixture/Program.fs resolves next to the fsproj parent`` () =
    let root = Path.Combine(Path.GetTempPath(), "fspure-uri", Guid.NewGuid().ToString("N"))
    let projDir = Path.Combine(root, "src", "App", "Lib")
    Directory.CreateDirectory projDir |> ignore
    let fs = Path.Combine(projDir, "Program.fs")
    File.WriteAllText(fs, "module X\n")

    let sarif =
        """{"version":"2.1.0","runs":[{"results":[{"ruleId":"PURE002","message":{"text":"Function 'X.f' is not transitively pure."},"locations":[{"physicalLocation":{"artifactLocation":{"uri":"Lib/Program.fs"},"region":{"startLine":1,"startColumn":1,"endLine":1,"endColumn":2}}}]}]}]}"""

    try
        match SarifRead.parseRel projDir root sarif with
        | Error e -> Assert.Fail e
        | Ok items ->
            Assert.Equal(1, items.Length)
            Assert.Equal("src/App/Lib/Program.fs", items[0].File.Replace('\\', '/'))
    finally
        if Directory.Exists root then
            Directory.Delete(root, true)

[<Fact>]
let ``fullNameFromMessage handles both purity messages`` () =
    Assert.Equal(
        "Foo.bar",
        SarifRead.fullNameFromMessage "Function 'Foo.bar' is not transitively pure."
    )

    Assert.Equal("Foo.baz", SarifRead.fullNameFromMessage "Function 'Foo.baz' is transitively pure.")
    Assert.Equal("x", SarifRead.fullNameFromMessage "Call to 'x' is not known to be pure.")
    Assert.Equal("Y", SarifRead.callerFromMessage "Call to 'x' inside 'Y' is not known to be pure.")
    Assert.Equal("x", SarifRead.calleeFromMessage "Call to 'x' inside 'Y' is not known to be pure.")
