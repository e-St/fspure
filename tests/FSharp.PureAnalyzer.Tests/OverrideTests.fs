module FSharp.PureAnalyzer.Tests.OverrideTests

open System
open FSharp.PureAnalyzer
open FSharp.PureSchema
open Xunit

module private Ov =
    let make (useFoundational: bool) (add: string list) (remove: string list) : PureOverrides =
        {
            SchemaVersion = PureOverridesSchema.Current
            UseFoundational = useFoundational
            Add = add
            Remove = remove
            ContentHash = Guid.NewGuid().ToString("N")
        }

[<Fact>]
let ``override add makes method pure on foundational base`` () =
    let name = "Customer.Override.AddedPure"
    Assert.False(PureSet.contains name)

    let ov = Ov.make true [ name ] []
    let loaded = PureManifestLoader.loadFromPathsWithOverrides [] (Some ov)

    Assert.True(loaded.OverridesApplied)
    Assert.True(loaded.UsedFoundational)
    Assert.True(PureSet.containsIn loaded.Index name)
    Assert.True(PureSet.containsIn loaded.Index "Microsoft.FSharp.Core.Operators.op_PipeRight")

[<Fact>]
let ``override remove strips an added method from the index`` () =
    let name = "Customer.Override.TempPure"
    let withAdd = PureManifestLoader.loadFromPathsWithOverrides [] (Some(Ov.make true [ name ] []))
    Assert.True(PureSet.containsIn withAdd.Index name)

    let after = PureSet.applyOverrides withAdd.Index (Ov.make true [] [ name ])
    Assert.False(PureSet.containsIn after name)

[<Fact>]
let ``add wins over remove when both list the same name`` () =
    let name = "Customer.Override.AddWins"
    let ov = Ov.make true [ name ] [ name ]
    let idx = PureSet.applyOverrides (PureSet.foundationalIndex ()) ov
    Assert.True(PureSet.containsIn idx name)

[<Fact>]
let ``useFoundational false drops foundational names from index`` () =
    let pipe = "Microsoft.FSharp.Core.Operators.op_PipeRight"
    // Index exact set should not contain foundational names when base is empty,
    // but containsIn still returns true for hard-coded operators.
    let ov = Ov.make false [] []
    let loaded = PureManifestLoader.loadFromPathsWithOverrides [] (Some ov)

    Assert.False(loaded.UsedFoundational)
    Assert.False(loaded.Index.Exact.Contains pipe)
    // Operator special-case remains (by design) for F# syntax safety:
    Assert.True(PureSet.containsIn loaded.Index pipe)

[<Fact>]
let ``useFoundational false plus add only has override methods in exact set`` () =
    let name = "Only.My.Pure"
    let ov = Ov.make false [ name ] []
    let loaded = PureManifestLoader.loadFromPathsWithOverrides [] (Some ov)

    Assert.False(loaded.UsedFoundational)
    Assert.True(PureSet.containsIn loaded.Index name)
    Assert.True(loaded.Index.Exact.Contains name)
    Assert.False(loaded.Index.Exact.Contains "Microsoft.FSharp.Collections.ListModule.Map")

[<Fact>]
let ``classification findNonPure respects override add`` () =
    let lib = "Vendor.Lib.SecretPure"
    let wrapper = "App.useSecret"

    let callGraph = Map.ofList [ wrapper, [ lib ] ]
    let without = Analysis.findNonPure PureSet.contains callGraph Set.empty
    Assert.True(Set.contains wrapper without)

    let ov = Ov.make true [ lib ] []
    let loaded = PureManifestLoader.loadFromPathsWithOverrides [] (Some ov)
    let withOv = Analysis.findNonPure (PureSet.containsIn loaded.Index) callGraph Set.empty
    Assert.False(Set.contains wrapper withOv)

[<Fact>]
let ``cache returns same instance for identical override inputs`` () =
    PureSet.clearCompositionCache ()
    let ov = Ov.make true [ "Cache.Override.One" ] []
    // Stable hash for cache key
    let ovStable =
        { ov with
            ContentHash = "deadbeef"
        }

    let a = PureManifestLoader.loadFromPathsWithOverrides [] (Some ovStable)
    let b = PureManifestLoader.loadFromPathsWithOverrides [] (Some ovStable)
    Assert.True(Object.ReferenceEquals(a.Index, b.Index))
