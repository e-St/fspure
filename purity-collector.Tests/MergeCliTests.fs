/// Tests for the collector merge semantics used by --merge-with
/// (union of pureMethods; last file wins on fullName collisions).
module purity_collector.Tests.MergeCliTests

open System
open System.IO
open FSharp.PureSchema
open Xunit

module private Helpers =
    let writeTemp (name: string) (file: PureFile) : string =
        let dir =
            Path.Combine(Path.GetTempPath(), "fspure-collector-merge", Guid.NewGuid().ToString("N"))

        Directory.CreateDirectory(dir) |> ignore
        let path = Path.Combine(dir, name)
        PureFileIO.write path file
        path

    let mk (id: string) (names: string list) : PureFile =
        {
            SchemaVersion = SchemaVersion.Current
            PackageId = id
            PackageVersion = "0.0.1"
            GeneratedAt = DateTimeOffset.UtcNow
            Generator = "test"
            PureMethods =
                names
                |> List.map (fun n -> { FullName = n; Origin = Automatic })
        }

[<Fact>]
let ``load and merge two pure.json files produces exact full-name set`` () =
    let first = Helpers.mk "lib-a" [ "Lib.A.PureOne"; "Lib.Shared.Name" ]
    let second = Helpers.mk "lib-b" [ "Lib.B.PureTwo"; "Lib.Shared.Name" ]

    let path1 = Helpers.writeTemp "a.pure.json" first
    let path2 = Helpers.writeTemp "b.pure.json" second

    try
        match PureFileIO.load path1, PureFileIO.load path2 with
        | Error e, _ -> Assert.Fail(string e)
        | _, Error e -> Assert.Fail(string e)
        | Ok a, Ok b ->
            match PureFileIO.merge [ a; b ] with
            | Error msg -> Assert.Fail(msg)
            | Ok merged ->
                let names =
                    merged.PureMethods
                    |> List.map _.FullName
                    |> List.sort

                Assert.Equal<string list>(
                    [ "Lib.A.PureOne"; "Lib.B.PureTwo"; "Lib.Shared.Name" ],
                    names
                )
    finally
        for p in [ path1; path2 ] do
            match Path.GetDirectoryName p with
            | null -> ()
            | d when Directory.Exists d -> Directory.Delete(d, true)
            | _ -> ()

[<Fact>]
let ``last merge-with file wins for conflicting full names`` () =
    let baseFile =
        {
            SchemaVersion = SchemaVersion.Current
            PackageId = "collected"
            PackageVersion = "1.0.0"
            GeneratedAt = DateTimeOffset.UtcNow
            Generator = "collector"
            PureMethods =
                [
                    { FullName = "X.M"; Origin = Automatic }
                    { FullName = "X.Keep"; Origin = Automatic }
                ]
        }

    let extra =
        {
            SchemaVersion = SchemaVersion.Current
            PackageId = "extra"
            PackageVersion = "1.0.0"
            GeneratedAt = DateTimeOffset.UtcNow
            Generator = "manual"
            PureMethods =
                [
                    {
                        FullName = "X.M"
                        Origin = Manual(Some "author override")
                    }
                    { FullName = "X.Extra"; Origin = Automatic }
                ]
        }

    let merged = PureFileIO.mergeWith baseFile [ extra ]
    let byName = merged.PureMethods |> List.map (fun m -> m.FullName, m) |> Map.ofList

    let names = byName.Keys |> Seq.sort |> Seq.toList
    Assert.Equal<string list>([ "X.Extra"; "X.Keep"; "X.M" ], names)

    match byName["X.M"].Origin with
    | Manual(Some c) -> Assert.Equal("author override", c)
    | other -> Assert.Fail($"expected author override, got {other}")

    Assert.Equal("collected", merged.PackageId)
