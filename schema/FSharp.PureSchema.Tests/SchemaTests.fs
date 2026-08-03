module FSharp.PureSchema.Tests.SchemaTests

open System
open System.IO
open FSharp.PureSchema
open Xunit

module private Samples =
    let validFile: PureFile =
        {
            SchemaVersion = SchemaVersion.Current
            PackageId = "Test.Package"
            PackageVersion = "1.2.3"
            GeneratedAt = DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero)
            Generator = "fsharp-pure-analyzer/purity-collector/0.1.0"
            PureMethods =
                [
                    { FullName = "MyLib.Math.Add"; Origin = Automatic }
                    {
                        FullName = "MyLib.Math.Custom"
                        Origin = Manual(Some "reviewed pure")
                    }
                ]
        }

    let validJson =
        """{
  "schemaVersion": "1.0",
  "packageId": "Test.Package",
  "packageVersion": "1.2.3",
  "generatedAt": "2026-01-15T12:00:00.0000000+00:00",
  "generator": "fsharp-pure-analyzer/purity-collector/0.1.0",
  "pureMethods": [
    { "fullName": "MyLib.Math.Add", "origin": "automatic" },
    { "fullName": "MyLib.Math.Custom", "origin": "manual", "comment": "reviewed pure" }
  ]
}"""

[<Fact>]
let ``round-trip serializes and parses a valid PureFile`` () =
    let json = PureFileIO.serialize Samples.validFile
    let result = PureFileIO.parse json

    match result with
    | Error e -> Assert.Fail($"expected Ok, got Error: {e}")
    | Ok file ->
        Assert.Equal(SchemaVersion.Current, file.SchemaVersion)
        Assert.Equal("Test.Package", file.PackageId)
        Assert.Equal("1.2.3", file.PackageVersion)
        Assert.Equal("fsharp-pure-analyzer/purity-collector/0.1.0", file.Generator)
        Assert.Equal(2, file.PureMethods.Length)

        let names = file.PureMethods |> List.map _.FullName
        Assert.Contains("MyLib.Math.Add", names)
        Assert.Contains("MyLib.Math.Custom", names)

        match file.PureMethods |> List.find (fun m -> m.FullName = "MyLib.Math.Custom") with
        | { Origin = Manual(Some c) } -> Assert.Equal("reviewed pure", c)
        | other -> Assert.Fail($"expected Manual with comment, got {other}")

[<Fact>]
let ``round-trip via write and load on disk`` () =
    let dir = Path.Combine(Path.GetTempPath(), "fspure-schema-tests", Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory(dir) |> ignore
    let path = Path.Combine(dir, "sample.pure.json")

    try
        PureFileIO.write path Samples.validFile

        match PureFileIO.load path with
        | Error e -> Assert.Fail($"expected Ok, got Error: {e}")
        | Ok file ->
            Assert.Equal(Samples.validFile.PackageId, file.PackageId)
            Assert.Equal(Samples.validFile.PureMethods.Length, file.PureMethods.Length)
    finally
        if Directory.Exists dir then
            Directory.Delete(dir, true)

[<Fact>]
let ``parse accepts a well-formed JSON document`` () =
    match PureFileIO.parse Samples.validJson with
    | Error e -> Assert.Fail($"expected Ok, got Error: {e}")
    | Ok file ->
        Assert.Equal("Test.Package", file.PackageId)
        Assert.Equal(2, file.PureMethods.Length)

[<Fact>]
let ``reject unknown schema version`` () =
    let json =
        Samples.validJson.Replace("\"schemaVersion\": \"1.0\"", "\"schemaVersion\": \"99.0\"")

    match PureFileIO.parse json with
    | Ok _ -> Assert.Fail("expected Error for unknown schema version")
    | Error(UnsupportedSchemaVersion v) -> Assert.Equal("99.0", v)
    | Error e -> Assert.Fail($"expected UnsupportedSchemaVersion, got {e}")

[<Fact>]
let ``reject newer schema version that is not supported`` () =
    let json =
        Samples.validJson.Replace("\"schemaVersion\": \"1.0\"", "\"schemaVersion\": \"1.1\"")

    match PureFileIO.parse json with
    | Ok _ -> Assert.Fail("expected Error for newer schema version")
    | Error(UnsupportedSchemaVersion v) -> Assert.Equal("1.1", v)
    | Error e -> Assert.Fail($"expected UnsupportedSchemaVersion, got {e}")

[<Fact>]
let ``reject missing schemaVersion`` () =
    let json =
        """{
  "packageId": "Test.Package",
  "packageVersion": "1.0.0",
  "generatedAt": "2026-01-15T12:00:00Z",
  "generator": "test",
  "pureMethods": []
}"""

    match PureFileIO.parse json with
    | Ok _ -> Assert.Fail("expected Error for missing schemaVersion")
    | Error(MissingRequiredField name) -> Assert.Equal("schemaVersion", name)
    | Error e -> Assert.Fail($"expected MissingRequiredField, got {e}")

[<Fact>]
let ``reject missing packageId`` () =
    let json =
        """{
  "schemaVersion": "1.0",
  "packageVersion": "1.0.0",
  "generatedAt": "2026-01-15T12:00:00Z",
  "generator": "test",
  "pureMethods": []
}"""

    match PureFileIO.parse json with
    | Ok _ -> Assert.Fail("expected Error for missing packageId")
    | Error(MissingRequiredField name) -> Assert.Equal("packageId", name)
    | Error e -> Assert.Fail($"expected MissingRequiredField, got {e}")

[<Fact>]
let ``reject missing pureMethods entry fullName`` () =
    let json =
        """{
  "schemaVersion": "1.0",
  "packageId": "Test.Package",
  "packageVersion": "1.0.0",
  "generatedAt": "2026-01-15T12:00:00Z",
  "generator": "test",
  "pureMethods": [
    { "origin": "automatic" }
  ]
}"""

    match PureFileIO.parse json with
    | Ok _ -> Assert.Fail("expected Error for missing fullName")
    | Error(MissingRequiredField name) -> Assert.Equal("fullName", name)
    | Error e -> Assert.Fail($"expected MissingRequiredField, got {e}")

[<Fact>]
let ``reject invalid JSON`` () =
    match PureFileIO.parse "{ not json" with
    | Ok _ -> Assert.Fail("expected Error for invalid JSON")
    | Error(InvalidJson _) -> ()
    | Error e -> Assert.Fail($"expected InvalidJson, got {e}")

[<Fact>]
let ``JSON Schema resource is embedded next to DTOs`` () =
    let asm = typeof<PureFile>.Assembly
    let names = asm.GetManifestResourceNames()

    Assert.Contains("FSharp.PureSchema.pure-file.schema.json", names)

    match asm.GetManifestResourceStream("FSharp.PureSchema.pure-file.schema.json") with
    | null -> Assert.Fail("embedded pure-file.schema.json stream was null")
    | stream ->
        use stream = stream
        use reader = new StreamReader(stream)
        let text = reader.ReadToEnd()
        Assert.Contains("schemaVersion", text)
        Assert.Contains("pureMethods", text)
