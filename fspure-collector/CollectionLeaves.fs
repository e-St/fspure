namespace FSharp.PureAnalyzer

open System

/// Explicit pure leaves for FSharp.Core collection / option combinators and
/// FSharpFunc Invoke plumbing. Kept separate from the large operator/BCL leaf set
/// so the HOF policy is reviewable and not an open-ended pattern.
module CollectionLeaves =

    let private mem modName names =
        names |> List.map (fun n -> modName + "." + n)

    /// Combinators that are pure when the user function is pure.
    /// Impurity of the lambda is attributed to the *caller* by the editor analyzer.
    let private listLike =
        [
            "Length"; "IsEmpty"; "Empty"; "Head"; "TryHead"; "Tail"; "Last"; "TryLast"
            "Item"; "TryItem"; "ExactlyOne"; "TryExactlyOne"; "Indexed"
            "Singleton"; "Replicate"; "Init"; "Unfold"; "OfArray"; "OfSeq"; "OfList"
            "ToArray"; "ToSeq"; "ToList"
            "Map"; "MapIndexed"; "Map2"; "Map3"; "MapFold"; "MapFoldBack"
            "Filter"; "Where"; "Choose"; "Collect"; "Concat"; "Append"
            "Exists"; "ForAll"; "Forall"; "Contains"; "Find"; "TryFind"
            "FindIndex"; "TryFindIndex"; "FindBack"; "TryFindBack"
            "FindIndexBack"; "TryFindIndexBack"; "Pick"; "TryPick"
            "Fold"; "FoldBack"; "Fold2"; "FoldBack2"; "Reduce"; "ReduceBack"
            "Scan"; "ScanBack"; "Sort"; "SortBy"; "SortWith"; "SortDescending"; "SortByDescending"
            "Rev"; "Distinct"; "DistinctBy"; "GroupBy"; "CountBy"; "Partition"; "SplitAt"
            "Zip"; "Zip3"; "Unzip"; "Unzip3"; "AllPairs"; "Pairwise"; "Windowed"; "ChunkBySize"
            "Take"; "TakeWhile"; "Skip"; "SkipWhile"; "Truncate"; "Except"; "Intersect"
            "Sum"; "SumBy"; "Average"; "AverageBy"; "Min"; "Max"; "MinBy"; "MaxBy"
            "CompareWith"; "Permute"; "InsertAt"; "RemoveAt"; "UpdateAt"; "InsertManyAt"; "RemoveManyAt"
        ]

    let private arrayLike =
        listLike
        @ [ "ZeroCreate"; "Create"; "Copy"; "Sub"; "GetSubArray" ]
        // intentionally no Set / Fill / Blit / Clear

    let private seqLike =
        listLike @ [ "InitInfinite"; "Cache"; "Delay"; "Readonly" ]

    let private mapLike =
        [
            "Empty"; "IsEmpty"; "Count"; "ContainsKey"; "ContainsValue"
            "Find"; "TryFind"; "FindKey"; "TryFindKey"; "Item"
            "Add"; "Change"; "Remove"; "Map"; "Filter"; "Partition"
            "Exists"; "ForAll"; "Forall"; "Fold"; "FoldBack"
            "ToList"; "ToArray"; "ToSeq"; "OfList"; "OfArray"; "OfSeq"
            "Keys"; "Values"; "MinKeyValue"; "MaxKeyValue"
            "TryMinKeyValue"; "TryMaxKeyValue"
        ]

    let private setLike =
        [
            "Empty"; "IsEmpty"; "Count"; "Contains"; "Add"; "Remove"
            "Singleton"; "Union"; "Intersect"; "Difference"
            "IsSubset"; "IsSuperset"; "IsProperSubset"; "IsProperSuperset"
            "Map"; "Filter"; "Partition"; "Exists"; "ForAll"; "Forall"
            "Fold"; "FoldBack"; "MinElement"; "MaxElement"
            "ToList"; "ToArray"; "ToSeq"; "OfList"; "OfArray"; "OfSeq"
        ]

    let private optionLike =
        [
            "Map"; "Bind"; "Exists"; "ForAll"; "Forall"; "Filter"; "Flatten"
            "IsSome"; "IsNone"; "DefaultValue"; "DefaultWith"; "OrElse"; "OrElseWith"
            "ToArray"; "ToList"; "ToSeq"; "OfNullable"; "ToNullable"; "Count"
            "Fold"; "FoldBack"; "Contains"
        ]

    let private resultLike =
        [
            "Map"; "MapError"; "Bind"; "IsOk"; "IsError"
            "DefaultValue"; "DefaultWith"; "Exists"; "ForAll"; "Forall"
        ]

    let private prefixes =
        [
            "Microsoft.FSharp.Collections.ListModule", listLike
            "Microsoft.FSharp.Collections.ArrayModule", arrayLike
            "Microsoft.FSharp.Collections.SeqModule", seqLike
            "Microsoft.FSharp.Collections.MapModule", mapLike
            "Microsoft.FSharp.Collections.SetModule", setLike
            "Microsoft.FSharp.Core.OptionModule", optionLike
            "Microsoft.FSharp.Core.ResultModule", resultLike
        ]

    /// Explicit Invoke leaves (definition names only — not reflection Invoke).
    let private invokeLeaves =
        [
            "Microsoft.FSharp.Core.FSharpFunc`2.Invoke"
            "Microsoft.FSharp.Core.FSharpFunc`2.InvokeFast"
            "Microsoft.FSharp.Core.OptimizedClosures.FSharpFunc`3.Invoke"
            "Microsoft.FSharp.Core.OptimizedClosures.FSharpFunc`4.Invoke"
            "Microsoft.FSharp.Core.OptimizedClosures.FSharpFunc`5.Invoke"
            "Microsoft.FSharp.Core.OptimizedClosures.FSharpFunc`6.Invoke"
        ]

    let all: Set<string> =
        set
            [
                yield! invokeLeaves
                for modName, names in prefixes do
                    yield! mem modName names
            ]
