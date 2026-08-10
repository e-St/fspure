module FSharp.PureSchema.Tests.MergeTests

open System
open FSharp.PureSchema
open Xunit

module private Fixtures =
    let mk (packageId: string) (methods: (string * PureOrigin) list) : PureFile =
        {
            SchemaVersion = SchemaVersion.Current
            PackageId = packageId
            PackageVersion = "1.0.0"
            GeneratedAt = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            Generator = "test"
            PureMethods =
                methods
                |> List.map (fun (name, origin) -> { FullName = name; Origin = origin })
        }

    let auto name = name, Automatic
    let manual name comment = name, Manual(Some comment)

[<Fact>]
let ``merge of empty sequence fails`` () =
    match PureFileIO.merge [] with
    | Ok _ -> Assert.Fail("expected Error for empty merge")
    | Error msg -> Assert.Contains("at least one", msg)

[<Fact>]
let ``merge single file is identity on method set`` () =
    let a = Fixtures.mk "A" [ Fixtures.auto "Lib.A"; Fixtures.auto "Lib.B" ]

    match PureFileIO.merge [ a ] with
    | Error msg -> Assert.Fail(msg)
    | Ok merged ->
        let names = merged.PureMethods |> List.map _.FullName |> List.sort
        Assert.Equal<string list>([ "Lib.A"; "Lib.B" ], names)
        Assert.Equal("A", merged.PackageId)

[<Fact>]
let ``merge two files unions pureMethods and last file wins on conflict`` () =
    let first =
        Fixtures.mk
            "Base"
            [
                Fixtures.auto "Shared.Method"
                Fixtures.auto "Only.In.First"
            ]

    let second =
        Fixtures.mk
            "Extra"
            [
                Fixtures.manual "Shared.Method" "override from second"
                Fixtures.auto "Only.In.Second"
            ]

    match PureFileIO.merge [ first; second ] with
    | Error msg -> Assert.Fail(msg)
    | Ok merged ->
        let byName =
            merged.PureMethods
            |> List.map (fun m -> m.FullName, m)
            |> Map.ofList

        let names = byName |> Map.toList |> List.map fst |> List.sort

        Assert.Equal<string list>(
            [ "Only.In.First"; "Only.In.Second"; "Shared.Method" ],
            names
        )

        // Metadata from the first file
        Assert.Equal("Base", merged.PackageId)

        match byName["Shared.Method"].Origin with
        | Manual(Some c) -> Assert.Equal("override from second", c)
        | other -> Assert.Fail($"expected last-wins Manual, got {other}")

[<Fact>]
let ``merge three files applies last-wins across the full chain`` () =
    let f1 = Fixtures.mk "P1" [ Fixtures.auto "M"; Fixtures.auto "A" ]
    let f2 = Fixtures.mk "P2" [ Fixtures.manual "M" "from-2"; Fixtures.auto "B" ]
    let f3 = Fixtures.mk "P3" [ Fixtures.manual "M" "from-3"; Fixtures.auto "C" ]

    match PureFileIO.merge [ f1; f2; f3 ] with
    | Error msg -> Assert.Fail(msg)
    | Ok merged ->
        let names = merged.PureMethods |> List.map _.FullName |> List.sort
        Assert.Equal<string list>([ "A"; "B"; "C"; "M" ], names)

        match merged.PureMethods |> List.find (fun m -> m.FullName = "M") with
        | { Origin = Manual(Some c) } -> Assert.Equal("from-3", c)
        | other -> Assert.Fail($"expected from-3, got {other}")

        Assert.Equal("P1", merged.PackageId)

[<Fact>]
let ``mergeWith appends additional files after the base`` () =
    let baseFile = Fixtures.mk "Base" [ Fixtures.auto "Base.Only" ]
    let extra = Fixtures.mk "Extra" [ Fixtures.auto "Extra.Only" ]

    let merged = PureFileIO.mergeWith baseFile [ extra ]
    let names = merged.PureMethods |> List.map _.FullName |> List.sort
    Assert.Equal<string list>([ "Base.Only"; "Extra.Only" ], names)
    Assert.Equal("Base", merged.PackageId)

[<Fact>]
let ``merged method list is sorted by full name`` () =
    let a = Fixtures.mk "A" [ Fixtures.auto "Z.Last"; Fixtures.auto "A.First" ]
    let b = Fixtures.mk "B" [ Fixtures.auto "M.Mid" ]

    match PureFileIO.merge [ a; b ] with
    | Error msg -> Assert.Fail(msg)
    | Ok merged ->
        let names = merged.PureMethods |> List.map _.FullName
        Assert.Equal<string list>([ "A.First"; "M.Mid"; "Z.Last" ], names)
