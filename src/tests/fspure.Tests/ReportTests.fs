module fspure.Tests.ReportTests

open System.Text
open Fspure.Cli
open Xunit

let private items =
    [
        {
            Code = "PURE001"
            File = "src/Core/B.fs"
            StartLine = 3
            StartColumn = 5
            EndLine = 3
            EndColumn = 9
            Message = "Call to 'App.Log.write' inside 'App.B.go' is not known to be pure."
            FullName = "App.Log.write"
            Caller = "App.B.go"
            Callee = "App.Log.write"
        }
        {
            Code = "PURE001"
            File = "src/Core/A.fs"
            StartLine = 1
            StartColumn = 5
            EndLine = 1
            EndColumn = 8
            Message = "Call to 'System.IO.File.ReadAllText' inside 'App.A.load' is not known to be pure."
            FullName = "System.IO.File.ReadAllText"
            Caller = "App.A.load"
            Callee = "System.IO.File.ReadAllText"
        }
        {
            Code = "PURE002"
            File = "src/Core/B.fs"
            StartLine = 1
            StartColumn = 5
            EndLine = 1
            EndColumn = 8
            Message = "Function 'App.B.go' is not transitively pure."
            FullName = "App.B.go"
            Caller = ""
            Callee = ""
        }
    ]

[<Fact>]
let ``json is byte-identical across two renders`` () =
    let a = Report.writeJson "MyApp.fsproj" [ "src/Core" ] [] true items
    let b = Report.writeJson "MyApp.fsproj" [ "src/Core" ] [] true items
    Assert.Equal<byte[]>(a, b)

[<Fact>]
let ``json sorts impure calls by file then line`` () =
    let json = Encoding.UTF8.GetString(Report.writeJson "p.fsproj" [] [] false items)
    let iA = json.IndexOf "src/Core/A.fs"
    let iB = json.IndexOf "src/Core/B.fs"
    Assert.True(iA > 0 && iB > iA, json)

[<Fact>]
let ``json lists caller and callee facts, not move advice`` () =
    let json = Encoding.UTF8.GetString(Report.writeJson "p.fsproj" [] [] true items)
    Assert.Contains("\"impureCalls\":2", json)
    Assert.Contains("\"affectedCallers\":2", json)
    Assert.Contains("\"failOnImpure\":true", json)
    Assert.Contains("\"caller\":\"App.B.go\"", json)
    Assert.Contains("\"callee\":\"App.Log.write\"", json)
    Assert.DoesNotContain("generatedAt", json)
    Assert.DoesNotContain("move", json)
    Assert.DoesNotContain("boundary", json)

[<Fact>]
let ``sarif is byte-identical across two renders`` () =
    let a = Report.writeSarif "MyApp.fsproj" items
    let b = Report.writeSarif "MyApp.fsproj" items
    Assert.Equal<byte[]>(a, b)
    let text = Encoding.UTF8.GetString a
    Assert.Contains("\"ruleId\":\"PURE001\"", text)
    Assert.Contains("\"caller\":\"App.B.go\"", text)
    Assert.DoesNotContain("startTimeUtc", text)

[<Fact>]
let ``focus list is sorted in the json envelope`` () =
    let json =
        Encoding.UTF8.GetString(Report.writeJson "p.fsproj" [ "z"; "a" ] [ "m"; "b" ] false [])

    Assert.Contains("\"focus\":[\"a\",\"z\"]", json)
    Assert.Contains("\"ignore\":[\"b\",\"m\"]", json)
