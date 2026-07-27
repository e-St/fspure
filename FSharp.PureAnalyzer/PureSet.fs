namespace FSharp.PureAnalyzer

open System
open System.Collections.Generic
open System.IO
open System.Reflection
open System.Text
open System.Text.Json

/// DTO for the embedded foundational.pure.json resource.
[<CLIMutable>]
type PureMethodDto =
    {
        fullName: string
        origin: string
        comment: string
    }

[<CLIMutable>]
type PureFileDto =
    {
        schemaVersion: string
        packageId: string
        packageVersion: string
        generatedAt: string
        generator: string
        pureMethods: PureMethodDto array
    }

/// Cached access to the embedded foundational pure set, with lookup that tolerates
/// FCS vs IL naming differences and includes a large supplemental pure allowlist
/// for FSharp.Core collection combinators (HOFs the IL fixed-point often drops).
module PureSet =

    let normalizeName (fullName: string) : string =
        let noSig =
            match fullName.IndexOf '(' with
            | -1 -> fullName
            | i -> fullName.Substring(0, i)

        let builder = StringBuilder(noSig.Length)
        let mutable i = 0

        while i < noSig.Length do
            let c = noSig[i]

            if c = '`' then
                i <- i + 1

                while i < noSig.Length && Char.IsDigit noSig[i] do
                    i <- i + 1
            else
                builder.Append c |> ignore
                i <- i + 1

        builder.ToString()

    let private lastSegmentKey (fullName: string) =
        let n = normalizeName fullName
        let i = n.LastIndexOf '.'
        let typePart = if i < 0 then "" else n.Substring(0, i)
        let memberPart = if i < 0 then n else n.Substring(i + 1)
        typePart.ToLowerInvariant() + "." + memberPart.ToLowerInvariant()

    /// Expand a module prefix with member names into full names.
    let private members (moduleName: string) (names: string list) =
        names |> List.map (fun n -> moduleName + "." + n)

    /// Pure FSharp.Core surface used as a safety net when foundational.pure.json
    /// was generated without treating FSharpFunc.Invoke as a pure leaf.
    /// Excludes clearly effectful APIs (iter*, mutating Array setters, etc.).
    let private supplementalLeaves: string array =
        let listMembers =
            [
                // queries / shape
                "Length"; "IsEmpty"; "Empty"; "Head"; "TryHead"; "Tail"; "Last"; "TryLast"
                "Item"; "TryItem"; "ExactlyOne"; "TryExactlyOne"; "Indexed"
                // constructors / conversion
                "Singleton"; "Replicate"; "Init"; "Unfold"; "OfArray"; "OfSeq"; "OfList"
                "ToArray"; "ToSeq"; "ToList"
                // transforms (HOFs)
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

        let arrayMembers =
            [
                "Length"; "IsEmpty"; "Empty"; "ZeroCreate"; "Create"; "Init"; "Replicate"
                "Head"; "TryHead"; "Last"; "TryLast"; "Item"; "TryItem"; "ExactlyOne"; "TryExactlyOne"
                "Indexed"; "OfList"; "OfSeq"; "OfArray"; "ToList"; "ToSeq"; "ToArray"
                "Map"; "MapIndexed"; "Map2"; "Map3"; "MapFold"; "MapFoldBack"
                "Filter"; "Where"; "Choose"; "Collect"; "Concat"; "Append"
                "Exists"; "ForAll"; "Forall"; "Contains"; "Find"; "TryFind"
                "FindIndex"; "TryFindIndex"; "FindBack"; "TryFindBack"
                "FindIndexBack"; "TryFindIndexBack"; "Pick"; "TryPick"
                "Fold"; "FoldBack"; "Fold2"; "FoldBack2"; "Reduce"; "ReduceBack"
                "Scan"; "ScanBack"; "Sort"; "SortBy"; "SortWith"; "SortDescending"; "SortByDescending"
                "Rev"; "Distinct"; "DistinctBy"; "GroupBy"; "CountBy"; "Partition"; "SplitAt"
                "Zip"; "Zip3"; "Unzip"; "Unzip3"; "AllPairs"; "Pairwise"; "Windowed"; "ChunkBySize"
                "Take"; "TakeWhile"; "Skip"; "SkipWhile"; "Truncate"; "Except"
                "Sum"; "SumBy"; "Average"; "AverageBy"; "Min"; "Max"; "MinBy"; "MaxBy"
                "CompareWith"; "Permute"; "Copy"; "Sub"; "GetSubArray"
                // note: omit Set, Fill, Blit, Clear — mutating
            ]

        let seqMembers =
            [
                "Length"; "IsEmpty"; "Empty"; "Singleton"; "Init"; "InitInfinite"; "Unfold"; "Replicate"
                "Head"; "TryHead"; "Last"; "TryLast"; "Item"; "TryItem"; "ExactlyOne"; "TryExactlyOne"
                "Indexed"; "OfList"; "OfArray"; "OfSeq"; "ToList"; "ToArray"; "ToSeq"
                "Map"; "MapIndexed"; "Map2"; "Map3"; "MapFold"; "MapFoldBack"
                "Filter"; "Where"; "Choose"; "Collect"; "Concat"; "Append"
                "Exists"; "ForAll"; "Forall"; "Contains"; "Find"; "TryFind"
                "FindIndex"; "TryFindIndex"; "FindBack"; "TryFindBack"
                "FindIndexBack"; "TryFindIndexBack"; "Pick"; "TryPick"
                "Fold"; "FoldBack"; "Fold2"; "FoldBack2"; "Reduce"; "ReduceBack"
                "Scan"; "ScanBack"; "Sort"; "SortBy"; "SortWith"; "SortDescending"; "SortByDescending"
                "Rev"; "Distinct"; "DistinctBy"; "GroupBy"; "CountBy"; "Partition"
                "Zip"; "Zip3"; "Unzip"; "Unzip3"; "AllPairs"; "Pairwise"; "Windowed"; "ChunkBySize"
                "Take"; "TakeWhile"; "Skip"; "SkipWhile"; "Truncate"; "Except"; "Intersect"
                "Sum"; "SumBy"; "Average"; "AverageBy"; "Min"; "Max"; "MinBy"; "MaxBy"
                "CompareWith"; "Cache"; "Delay"; "Readonly"
            ]

        let mapMembers =
            [
                "Empty"; "IsEmpty"; "Count"; "ContainsKey"; "ContainsValue"
                "Find"; "TryFind"; "FindKey"; "TryFindKey"; "Item"
                "Add"; "Change"; "Remove"; "Map"; "Filter"; "Partition"
                "Exists"; "ForAll"; "Forall"; "Fold"; "FoldBack"
                "ToList"; "ToArray"; "ToSeq"; "OfList"; "OfArray"; "OfSeq"
                "Keys"; "Values"; "MinKeyValue"; "MaxKeyValue"
                "TryMinKeyValue"; "TryMaxKeyValue"
            ]

        let setMembers =
            [
                "Empty"; "IsEmpty"; "Count"; "Contains"; "Add"; "Remove"
                "Singleton"; "Union"; "Intersect"; "Difference"; "IsSubset"; "IsSuperset"
                "IsProperSubset"; "IsProperSuperset"
                "Map"; "Filter"; "Partition"; "Exists"; "ForAll"; "Forall"
                "Fold"; "FoldBack"; "MinElement"; "MaxElement"
                "ToList"; "ToArray"; "ToSeq"; "OfList"; "OfArray"; "OfSeq"
            ]

        let optionMembers =
            [
                "Map"; "Bind"; "Exists"; "ForAll"; "Forall"; "Filter"; "Flatten"
                "IsSome"; "IsNone"; "DefaultValue"; "DefaultWith"; "OrElse"; "OrElseWith"
                "ToArray"; "ToList"; "ToSeq"; "OfNullable"; "ToNullable"; "Count"
                "Fold"; "FoldBack"; "Contains"; "Iter" // Iter only pure if f pure; walker attributes f to caller
            ]

        let resultMembers =
            [
                "Map"; "MapError"; "Bind"; "IsOk"; "IsError"
                "DefaultValue"; "DefaultWith"; "Exists"; "ForAll"; "Forall"
            ]

        let invokePlumbing =
            [
                "Microsoft.FSharp.Core.FSharpFunc.Invoke"
                "Microsoft.FSharp.Core.FSharpFunc.InvokeFast"
                "Microsoft.FSharp.Core.OptimizedClosures.FSharpFunc.Invoke"
            ]

        let prefixes =
            [
                "Microsoft.FSharp.Collections.ListModule", listMembers
                "Microsoft.FSharp.Collections.List", listMembers
                "Microsoft.FSharp.Collections.ArrayModule", arrayMembers
                "Microsoft.FSharp.Collections.Array", arrayMembers
                "Microsoft.FSharp.Collections.SeqModule", seqMembers
                "Microsoft.FSharp.Collections.Seq", seqMembers
                "Microsoft.FSharp.Collections.MapModule", mapMembers
                "Microsoft.FSharp.Collections.Map", mapMembers
                "Microsoft.FSharp.Collections.SetModule", setMembers
                "Microsoft.FSharp.Collections.Set", setMembers
                "Microsoft.FSharp.Core.OptionModule", optionMembers
                "Microsoft.FSharp.Core.Option", optionMembers
                "Microsoft.FSharp.Core.ResultModule", resultMembers
                "Microsoft.FSharp.Core.Result", resultMembers
            ]

        [|
            yield! invokePlumbing
            for modName, names in prefixes do
                yield! members modName names
        |]

    let private isFunctionInvokeLeaf (normalized: string) =
        let n = normalized
        // Deny reflection / delegate Invoke (same policy as collector)
        if n.StartsWith("System.Reflection", StringComparison.Ordinal)
           || n.StartsWith("System.Delegate", StringComparison.Ordinal)
           || n.Contains("DynamicInvoke", StringComparison.Ordinal)
           || n.Contains("InvokeMember", StringComparison.Ordinal) then
            false
        else
            let isFsharpFunc =
                n.Contains("Microsoft.FSharp.Core.FSharpFunc", StringComparison.Ordinal)
                || n.Contains("Microsoft.FSharp.Core.OptimizedClosures", StringComparison.Ordinal)

            let isInvoke =
                n.EndsWith(".Invoke", StringComparison.Ordinal)
                || n.Contains(".InvokeFast", StringComparison.Ordinal)

            isFsharpFunc && isInvoke

    let private loadResource () =
        let assembly = Assembly.GetExecutingAssembly()

        let resourceName =
            assembly.GetManifestResourceNames()
            |> Array.tryFind (fun n -> n.EndsWith("foundational.pure.json", StringComparison.OrdinalIgnoreCase))

        match resourceName with
        | None -> failwith "Embedded resource 'foundational.pure.json' was not found."
        | Some name ->
            match assembly.GetManifestResourceStream(name) with
            | null -> failwith $"Unable to open embedded resource '%s{name}'."
            | stream ->
                use reader = new StreamReader(stream)
                reader.ReadToEnd()

    type private PureIndex =
        {
            Exact: HashSet<string>
            Normalized: HashSet<string>
            LastSegment: HashSet<string>
        }

    let private buildIndex (names: seq<string>) =
        let exact = HashSet<string>(StringComparer.Ordinal)
        let normalized = HashSet<string>(StringComparer.Ordinal)
        let lastSeg = HashSet<string>(StringComparer.Ordinal)

        for fn in names do
            if not (String.IsNullOrWhiteSpace fn) then
                exact.Add(fn) |> ignore
                let n = normalizeName fn
                normalized.Add(n) |> ignore
                lastSeg.Add(lastSegmentKey fn) |> ignore

        { Exact = exact; Normalized = normalized; LastSegment = lastSeg }

    let private parsedIndex =
        lazy
            let json = loadResource ()

            let options =
                JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)

            let fromJson =
                match JsonSerializer.Deserialize<PureFileDto>(json, options) with
                | null -> failwith "Failed to deserialize foundational.pure.json."
                | dto ->
                    dto.pureMethods
                    |> Array.map (fun m -> m.fullName)
                    |> Seq.ofArray

            buildIndex (Seq.append fromJson supplementalLeaves)

    let contains (fullName: string) : bool =
        if isFunctionInvokeLeaf (normalizeName fullName) then
            true
        else
            let idx = parsedIndex.Value

            if idx.Exact.Contains(fullName) then
                true
            else
                let n = normalizeName fullName

                if idx.Normalized.Contains(n) || idx.Exact.Contains(n) then
                    true
                else
                    idx.LastSegment.Contains(lastSegmentKey fullName)

    let knownPure: IReadOnlySet<string> =
        parsedIndex.Value.Exact
