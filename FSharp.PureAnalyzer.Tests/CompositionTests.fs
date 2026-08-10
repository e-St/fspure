module FSharp.PureAnalyzer.Tests.CompositionTests

open System
open FSharp.PureAnalyzer
open FSharp.PureSchema
open Xunit

module private Samples =
    let pureFile (packageId: string) (names: string list) : PureFile =
        {
            SchemaVersion = SchemaVersion.Current
            PackageId = packageId
            PackageVersion = "1.0.0"
            GeneratedAt = DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero)
            Generator = "phase1-tests"
            PureMethods =
                names
                |> List.map (fun n -> { FullName = n; Origin = Automatic })
        }

[<Fact>]
let ``foundational contains still honours core APIs and operators`` () =
    // Live analyser path remains foundational-only.
    Assert.True(PureSet.contains "Microsoft.FSharp.Core.Operators.op_PipeRight")
    Assert.True(PureSet.contains "Microsoft.FSharp.Collections.ListModule.Map")

[<Fact>]
let ``names only in additional PureFiles are visible after compose`` () =
    let onlyInLib = "Customer.Lib.Math.Add"

    Assert.False(PureSet.contains onlyInLib)

    let extra = Samples.pureFile "Customer.Lib" [ onlyInLib; "Customer.Lib.Math.Mul" ]
    let composed = PureSet.composeWithFoundational [ extra ]

    Assert.True(PureSet.containsIn composed onlyInLib)
    Assert.True(PureSet.containsIn composed "Customer.Lib.Math.Mul")
    // Foundational still present on the composed index
    Assert.True(PureSet.containsIn composed "Microsoft.FSharp.Core.Operators.op_PipeRight")
    // Global contains() remains foundational-only
    Assert.False(PureSet.contains onlyInLib)

[<Fact>]
let ``compose with multiple PureFiles unions all method names`` () =
    let a = Samples.pureFile "A" [ "Lib.A.One" ]
    let b = Samples.pureFile "B" [ "Lib.B.Two"; "Lib.Shared" ]
    let composed = PureSet.composeWithFoundational [ a; b ]

    Assert.True(PureSet.containsIn composed "Lib.A.One")
    Assert.True(PureSet.containsIn composed "Lib.B.Two")
    Assert.True(PureSet.containsIn composed "Lib.Shared")

[<Fact>]
let ``compose empty additional returns same foundational index instance`` () =
    let baseIdx = PureSet.foundationalIndex ()
    let composed = PureSet.compose baseIdx []
    Assert.True(Object.ReferenceEquals(baseIdx, composed))

[<Fact>]
let ``cache returns same Index instance for identical inputs`` () =
    PureSet.clearCompositionCache ()

    let mvid = Guid("11111111-2222-3333-4444-555555555555")
    let content = """{"schemaVersion":"1.0"}"""
    let hash = PureResourceReader.contentHash content
    let key = PureSet.makeCompositionCacheKey [ mvid, [ hash ] ]

    let extra = Samples.pureFile "Cached.Lib" [ "Cached.Lib.PureMethod" ]
    let baseIdx = PureSet.foundationalIndex ()

    let first = PureSet.getOrComposeCached key baseIdx [ extra ]
    let second = PureSet.getOrComposeCached key baseIdx [ extra ]

    Assert.True(Object.ReferenceEquals(first, second))
    Assert.True(PureSet.containsIn first "Cached.Lib.PureMethod")
    Assert.Equal(1, PureSet.compositionCacheCount ())

[<Fact>]
let ``different cache keys produce distinct Index instances`` () =
    PureSet.clearCompositionCache ()

    let mvid = Guid("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")
    let key1 = PureSet.makeCompositionCacheKey [ mvid, [ "hash-one" ] ]
    let key2 = PureSet.makeCompositionCacheKey [ mvid, [ "hash-two" ] ]

    let a = Samples.pureFile "A" [ "A.Only" ]
    let b = Samples.pureFile "B" [ "B.Only" ]
    let baseIdx = PureSet.foundationalIndex ()

    let idx1 = PureSet.getOrComposeCached key1 baseIdx [ a ]
    let idx2 = PureSet.getOrComposeCached key2 baseIdx [ b ]

    Assert.False(Object.ReferenceEquals(idx1, idx2))
    Assert.True(PureSet.containsIn idx1 "A.Only")
    Assert.False(PureSet.containsIn idx1 "B.Only")
    Assert.True(PureSet.containsIn idx2 "B.Only")
    // Cache is process-global; other test classes may insert entries concurrently.
    Assert.True(PureSet.compositionCacheCount () >= 2)

[<Fact>]
let ``composed index preserves name normalisation for additional methods`` () =
    // Index stores normalized forms; containsIn strips arity markers like `1.
    let extra = Samples.pureFile "Norm" [ "MyLib.Module`1.Map" ]
    let composed = PureSet.composeWithFoundational [ extra ]

    Assert.True(PureSet.containsIn composed "MyLib.Module`1.Map")
    Assert.True(PureSet.containsIn composed "MyLib.Module.Map")
