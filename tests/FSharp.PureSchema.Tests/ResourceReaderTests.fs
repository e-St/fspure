module FSharp.PureSchema.Tests.ResourceReaderTests

open System
open System.IO
open System.Reflection
open FSharp.PureSchema
open Xunit

module private Fixtures =
    /// Resolve a fixture assembly DLL that was ProjectReferenced and copied to output.
    let path (assemblyFileName: string) : string =
        let baseDir = AppContext.BaseDirectory

        let candidates =
            [
                Path.Combine(baseDir, assemblyFileName)
                Path.Combine(baseDir, "fixtures", assemblyFileName)
            ]

        match candidates |> List.tryFind File.Exists with
        | Some p -> p
        | None ->
            let asmLoc = Assembly.GetExecutingAssembly().Location

            let asmDir =
                if String.IsNullOrWhiteSpace asmLoc then
                    baseDir
                else
                    match Path.GetDirectoryName asmLoc with
                    | null
                    | "" -> baseDir
                    | d -> d

            let p = Path.Combine(asmDir, assemblyFileName)

            if File.Exists p then
                p
            else
                let searched = String.Join("; ", candidates)
                failwith $"Fixture assembly not found: {assemblyFileName}. Searched: {searched}"

[<Fact>]
let ``isPureJsonResourceName matches suffix and conventional names`` () =
    Assert.True(PureResourceReader.isPureJsonResourceName "Lib" "Lib.pure.json")
    Assert.True(PureResourceReader.isPureJsonResourceName "Lib" "Namespace.Lib.pure.json")
    Assert.True(PureResourceReader.isPureJsonResourceName "Lib" "anything.pure.json")
    Assert.False(PureResourceReader.isPureJsonResourceName "Lib" "Lib.data.json")
    Assert.False(PureResourceReader.isPureJsonResourceName "Lib" "notes.txt")

[<Fact>]
let ``zero pure.json resources yields empty list`` () =
    let path = Fixtures.path "ZeroPureResources.dll"

    match PureResourceReader.tryReadFromPath path with
    | Error e -> Assert.Fail e
    | Ok manifests ->
        Assert.Equal("ZeroPureResources", manifests.AssemblyName)
        Assert.NotEqual(Guid.Empty, manifests.Mvid)
        Assert.Empty(manifests.Resources)

[<Fact>]
let ``single correctly named pure.json is extracted and parsed`` () =
    let path = Fixtures.path "SinglePureResource.dll"

    match PureResourceReader.tryReadFromPath path with
    | Error e -> Assert.Fail e
    | Ok manifests ->
        Assert.Equal("SinglePureResource", manifests.AssemblyName)
        Assert.Equal(1, manifests.Resources.Length)

        let r = manifests.Resources.Head
        Assert.Equal("SinglePureResource.pure.json", r.ResourceName)
        Assert.False(String.IsNullOrWhiteSpace r.ContentHash)

        match r.PureFile with
        | Error e -> Assert.Fail(string e)
        | Ok file ->
            Assert.Equal(SchemaVersion.Current, file.SchemaVersion)
            Assert.Equal("SinglePureResource", file.PackageId)

            let names = file.PureMethods |> List.map _.FullName
            Assert.Equal<string list>([ "SinglePureResource.Api.OnlyInSingle" ], names)

        let parsed = PureResourceReader.parsedFiles manifests
        Assert.Equal(1, parsed.Length)

[<Fact>]
let ``multi resource assembly extracts only pure.json matches`` () =
    let path = Fixtures.path "MultiPureResources.dll"

    match PureResourceReader.tryReadFromPath path with
    | Error e -> Assert.Fail e
    | Ok manifests ->
        Assert.Equal("MultiPureResources", manifests.AssemblyName)

        let names = manifests.Resources |> List.map _.ResourceName |> List.sort

        Assert.Equal<string list>(
            [ "MultiPureResources.pure.json"; "Some.Namespace.extra.pure.json" ],
            names
        )

        // Non-matching notes.txt / data.json must not appear.
        Assert.DoesNotContain("MultiPureResources.notes.txt", names)
        Assert.DoesNotContain("MultiPureResources.data.json", names)

        let methodNames =
            PureResourceReader.parsedFiles manifests
            |> List.collect (fun f -> f.PureMethods |> List.map _.FullName)
            |> List.sort

        Assert.Equal<string list>(
            [ "MultiPureResources.Api.FromExtra"; "MultiPureResources.Api.FromMain" ],
            methodNames
        )

[<Fact>]
let ``tryReadFromStream matches tryReadFromPath`` () =
    let path = Fixtures.path "SinglePureResource.dll"

    use fs = File.OpenRead path

    match PureResourceReader.tryReadFromStream path fs with
    | Error e -> Assert.Fail e
    | Ok fromStream ->
        match PureResourceReader.tryReadFromPath path with
        | Error e -> Assert.Fail e
        | Ok fromPath ->
            Assert.Equal(fromPath.AssemblyName, fromStream.AssemblyName)
            Assert.Equal(fromPath.Mvid, fromStream.Mvid)
            Assert.Equal(fromPath.Resources.Length, fromStream.Resources.Length)

            Assert.Equal(
                fromPath.Resources.Head.ContentHash,
                fromStream.Resources.Head.ContentHash
            )

[<Fact>]
let ``cache key fragment is stable for same mvid and hashes`` () =
    let mvid = Guid("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")
    let a = PureResourceReader.cacheKeyFragment mvid [ "hash-b"; "hash-a" ]
    let b = PureResourceReader.cacheKeyFragment mvid [ "hash-a"; "hash-b" ]
    Assert.Equal(a, b)

    let key =
        PureResourceReader.compositionCacheKey
            [
                PureResourceReader.cacheKeyFragment mvid [ "h1" ]
                PureResourceReader.cacheKeyFragment (Guid.NewGuid()) [ "h2" ]
            ]

    Assert.False(String.IsNullOrWhiteSpace key)
