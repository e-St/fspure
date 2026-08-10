namespace FSharp.PureAnalyzer

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.IO
open System.Reflection
open System.Text
open FSharp.PureSchema

/// Cached access to the embedded foundational pure set, with lookup that tolerates
/// FCS vs IL naming differences and includes a large supplemental pure allowlist
/// for FSharp.Core collection combinators (HOFs the IL fixed-point often drops).
///
/// Composition + caching for foundational, library embeds, and project overrides.
/// Live analyser uses PureManifestLoader (overrides > embeds > foundational).
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
                "Length"
                "IsEmpty"
                "Empty"
                "Head"
                "TryHead"
                "Tail"
                "Last"
                "TryLast"
                "Item"
                "TryItem"
                "ExactlyOne"
                "TryExactlyOne"
                "Indexed"
                // constructors / conversion
                "Singleton"
                "Replicate"
                "Init"
                "Unfold"
                "OfArray"
                "OfSeq"
                "OfList"
                "ToArray"
                "ToSeq"
                "ToList"
                // transforms (HOFs)
                "Map"
                "MapIndexed"
                "Map2"
                "Map3"
                "MapFold"
                "MapFoldBack"
                "Filter"
                "Where"
                "Choose"
                "Collect"
                "Concat"
                "Append"
                "Exists"
                "ForAll"
                "Forall"
                "Contains"
                "Find"
                "TryFind"
                "FindIndex"
                "TryFindIndex"
                "FindBack"
                "TryFindBack"
                "FindIndexBack"
                "TryFindIndexBack"
                "Pick"
                "TryPick"
                "Fold"
                "FoldBack"
                "Fold2"
                "FoldBack2"
                "Reduce"
                "ReduceBack"
                "Scan"
                "ScanBack"
                "Sort"
                "SortBy"
                "SortWith"
                "SortDescending"
                "SortByDescending"
                "Rev"
                "Distinct"
                "DistinctBy"
                "GroupBy"
                "CountBy"
                "Partition"
                "SplitAt"
                "Zip"
                "Zip3"
                "Unzip"
                "Unzip3"
                "AllPairs"
                "Pairwise"
                "Windowed"
                "ChunkBySize"
                "Take"
                "TakeWhile"
                "Skip"
                "SkipWhile"
                "Truncate"
                "Except"
                "Intersect"
                "Sum"
                "SumBy"
                "Average"
                "AverageBy"
                "Min"
                "Max"
                "MinBy"
                "MaxBy"
                "CompareWith"
                "Permute"
                "InsertAt"
                "RemoveAt"
                "UpdateAt"
                "InsertManyAt"
                "RemoveManyAt"
            ]

        let arrayMembers =
            [
                "Length"
                "IsEmpty"
                "Empty"
                "ZeroCreate"
                "Create"
                "Init"
                "Replicate"
                "Head"
                "TryHead"
                "Last"
                "TryLast"
                "Item"
                "TryItem"
                "ExactlyOne"
                "TryExactlyOne"
                "Indexed"
                "OfList"
                "OfSeq"
                "OfArray"
                "ToList"
                "ToSeq"
                "ToArray"
                "Map"
                "MapIndexed"
                "Map2"
                "Map3"
                "MapFold"
                "MapFoldBack"
                "Filter"
                "Where"
                "Choose"
                "Collect"
                "Concat"
                "Append"
                "Exists"
                "ForAll"
                "Forall"
                "Contains"
                "Find"
                "TryFind"
                "FindIndex"
                "TryFindIndex"
                "FindBack"
                "TryFindBack"
                "FindIndexBack"
                "TryFindIndexBack"
                "Pick"
                "TryPick"
                "Fold"
                "FoldBack"
                "Fold2"
                "FoldBack2"
                "Reduce"
                "ReduceBack"
                "Scan"
                "ScanBack"
                "Sort"
                "SortBy"
                "SortWith"
                "SortDescending"
                "SortByDescending"
                "Rev"
                "Distinct"
                "DistinctBy"
                "GroupBy"
                "CountBy"
                "Partition"
                "SplitAt"
                "Zip"
                "Zip3"
                "Unzip"
                "Unzip3"
                "AllPairs"
                "Pairwise"
                "Windowed"
                "ChunkBySize"
                "Take"
                "TakeWhile"
                "Skip"
                "SkipWhile"
                "Truncate"
                "Except"
                "Sum"
                "SumBy"
                "Average"
                "AverageBy"
                "Min"
                "Max"
                "MinBy"
                "MaxBy"
                "CompareWith"
                "Permute"
                "Copy"
                "Sub"
                "GetSubArray"
            // note: omit Set, Fill, Blit, Clear — mutating
            ]

        let seqMembers =
            [
                "Length"
                "IsEmpty"
                "Empty"
                "Singleton"
                "Init"
                "InitInfinite"
                "Unfold"
                "Replicate"
                "Head"
                "TryHead"
                "Last"
                "TryLast"
                "Item"
                "TryItem"
                "ExactlyOne"
                "TryExactlyOne"
                "Indexed"
                "OfList"
                "OfArray"
                "OfSeq"
                "ToList"
                "ToArray"
                "ToSeq"
                "Map"
                "MapIndexed"
                "Map2"
                "Map3"
                "MapFold"
                "MapFoldBack"
                "Filter"
                "Where"
                "Choose"
                "Collect"
                "Concat"
                "Append"
                "Exists"
                "ForAll"
                "Forall"
                "Contains"
                "Find"
                "TryFind"
                "FindIndex"
                "TryFindIndex"
                "FindBack"
                "TryFindBack"
                "FindIndexBack"
                "TryFindIndexBack"
                "Pick"
                "TryPick"
                "Fold"
                "FoldBack"
                "Fold2"
                "FoldBack2"
                "Reduce"
                "ReduceBack"
                "Scan"
                "ScanBack"
                "Sort"
                "SortBy"
                "SortWith"
                "SortDescending"
                "SortByDescending"
                "Rev"
                "Distinct"
                "DistinctBy"
                "GroupBy"
                "CountBy"
                "Partition"
                "Zip"
                "Zip3"
                "Unzip"
                "Unzip3"
                "AllPairs"
                "Pairwise"
                "Windowed"
                "ChunkBySize"
                "Take"
                "TakeWhile"
                "Skip"
                "SkipWhile"
                "Truncate"
                "Except"
                "Intersect"
                "Sum"
                "SumBy"
                "Average"
                "AverageBy"
                "Min"
                "Max"
                "MinBy"
                "MaxBy"
                "CompareWith"
                "Cache"
                "Delay"
                "Readonly"
            ]

        let mapMembers =
            [
                "Empty"
                "IsEmpty"
                "Count"
                "ContainsKey"
                "ContainsValue"
                "Find"
                "TryFind"
                "FindKey"
                "TryFindKey"
                "Item"
                "Add"
                "Change"
                "Remove"
                "Map"
                "Filter"
                "Partition"
                "Exists"
                "ForAll"
                "Forall"
                "Fold"
                "FoldBack"
                "ToList"
                "ToArray"
                "ToSeq"
                "OfList"
                "OfArray"
                "OfSeq"
                "Keys"
                "Values"
                "MinKeyValue"
                "MaxKeyValue"
                "TryMinKeyValue"
                "TryMaxKeyValue"
            ]

        let setMembers =
            [
                "Empty"
                "IsEmpty"
                "Count"
                "Contains"
                "Add"
                "Remove"
                "Singleton"
                "Union"
                "Intersect"
                "Difference"
                "IsSubset"
                "IsSuperset"
                "IsProperSubset"
                "IsProperSuperset"
                "Map"
                "Filter"
                "Partition"
                "Exists"
                "ForAll"
                "Forall"
                "Fold"
                "FoldBack"
                "MinElement"
                "MaxElement"
                "ToList"
                "ToArray"
                "ToSeq"
                "OfList"
                "OfArray"
                "OfSeq"
            ]

        let optionMembers =
            [
                "Map"
                "Bind"
                "Exists"
                "ForAll"
                "Forall"
                "Filter"
                "Flatten"
                "IsSome"
                "IsNone"
                "DefaultValue"
                "DefaultWith"
                "OrElse"
                "OrElseWith"
                "ToArray"
                "ToList"
                "ToSeq"
                "OfNullable"
                "ToNullable"
                "Count"
                "Fold"
                "FoldBack"
                "Contains"
                "Iter" // Iter only pure if f pure; walker attributes f to caller
            ]

        let resultMembers =
            [
                "Map"
                "MapError"
                "Bind"
                "IsOk"
                "IsError"
                "DefaultValue"
                "DefaultWith"
                "Exists"
                "ForAll"
                "Forall"
            ]


        let pureOperators =
            [
                // Forward / backward pipe
                "Microsoft.FSharp.Core.Operators.op_PipeRight"
                "Microsoft.FSharp.Core.Operators.op_PipeLeft"
                // Function composition
                "Microsoft.FSharp.Core.Operators.op_ComposeRight"
                "Microsoft.FSharp.Core.Operators.op_ComposeLeft"
                // List cons / append
                "Microsoft.FSharp.Core.Operators.op_ColonColon"
                "Microsoft.FSharp.Core.Operators.op_Append"
                // Ranges
                "Microsoft.FSharp.Core.Operators.op_Range"
                "Microsoft.FSharp.Core.Operators.op_RangeStep"
                // Boolean
                "Microsoft.FSharp.Core.Operators.op_BooleanAnd"
                "Microsoft.FSharp.Core.Operators.op_BooleanOr"
                // Bitwise (pure)
                "Microsoft.FSharp.Core.Operators.op_BitwiseAnd"
                "Microsoft.FSharp.Core.Operators.op_BitwiseOr"
                "Microsoft.FSharp.Core.Operators.op_ExclusiveOr"
                "Microsoft.FSharp.Core.Operators.op_LogicalNot"
                "Microsoft.FSharp.Core.Operators.op_LeftShift"
                "Microsoft.FSharp.Core.Operators.op_RightShift"
                // Unary / misc pure
                "Microsoft.FSharp.Core.Operators.op_UnaryPlus"
                "Microsoft.FSharp.Core.Operators.op_UnaryNegation"
                "Microsoft.FSharp.Core.Operators.op_Concatenate" // string +
            // Note: op_ColonEquals (:=) is assignment — intentionally omitted
            // Note: op_Dereference (!) on refs is omitted (reads mutable cells)
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
            yield! pureOperators
            yield! invokePlumbing
            for modName, names in prefixes do
                yield! members modName names
        |]

    let private isFunctionInvokeLeaf (normalized: string) =
        let n = normalized
        // Deny reflection / delegate Invoke (same policy as collector)
        if
            n.StartsWith("System.Reflection", StringComparison.Ordinal)
            || n.StartsWith("System.Delegate", StringComparison.Ordinal)
            || n.Contains("DynamicInvoke", StringComparison.Ordinal)
            || n.Contains("InvokeMember", StringComparison.Ordinal)
        then
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

    /// Lookup index over pure method full names (exact, normalized, last-segment).
    /// Treat as opaque; construct only via PureSet helpers. Reference equality matters for cache hits.
    type Index =
        {
            Exact: HashSet<string>
            Normalized: HashSet<string>
            LastSegment: HashSet<string>
        }

    let private buildIndex (names: seq<string>) : Index =
        let exact = HashSet<string>(StringComparer.Ordinal)
        let normalized = HashSet<string>(StringComparer.Ordinal)
        let lastSeg = HashSet<string>(StringComparer.Ordinal)

        for fn in names do
            if not (String.IsNullOrWhiteSpace fn) then
                exact.Add(fn) |> ignore
                let n = normalizeName fn
                normalized.Add(n) |> ignore
                lastSeg.Add(lastSegmentKey fn) |> ignore

        {
            Exact = exact
            Normalized = normalized
            LastSegment = lastSeg
        }

    let private namesFromPureFiles (files: PureFile seq) : seq<string> =
        files
        |> Seq.collect (fun f -> f.PureMethods |> Seq.map _.FullName)

    let private foundationalIndexLazy =
        lazy
            let json = loadResource ()

            let fromJson =
                match PureFileIO.parse json with
                | Ok file -> file.PureMethods |> List.map _.FullName |> Seq.ofList
                | Error e -> failwith $"Failed to parse foundational.pure.json: {e}"

            buildIndex (Seq.append fromJson supplementalLeaves)

    /// Immutable foundational pure index (embedded foundational.pure.json + supplemental leaves).
    let foundationalIndex () : Index = foundationalIndexLazy.Value

    let private isKnownPureOperator (normalized: string) =
        let key = lastSegmentKey normalized
        // lastSegmentKey is "type.member" lowercased
        let memberName =
            let i = key.LastIndexOf '.'
            if i < 0 then key else key.Substring(i + 1)

        match memberName with
        | "op_piperight"
        | "op_pipeleft"
        | "op_composeright"
        | "op_composeleft"
        | "op_coloncolon"
        | "op_append"
        | "op_range"
        | "op_rangestep"
        | "op_booleanand"
        | "op_booleanor"
        | "op_bitwiseand"
        | "op_bitwiseor"
        | "op_exclusiveor"
        | "op_logicalnot"
        | "op_leftshift"
        | "op_rightshift"
        | "op_unaryplus"
        | "op_unarynegation"
        | "op_concatenate"
        // arithmetic / compare often already in JSON; keep as safety net
        | "op_addition"
        | "op_subtraction"
        | "op_multiply"
        | "op_division"
        | "op_modulus"
        | "op_equality"
        | "op_inequality"
        | "op_lessthan"
        | "op_greaterthan"
        | "op_lessthanorequal"
        | "op_greaterthanorequal" -> true
        | _ -> false

    /// Lookup using the given index, preserving foundational name-normalisation,
    /// last-segment, operator, and FSharpFunc.Invoke special cases.
    let containsIn (index: Index) (fullName: string) : bool =
        let n = normalizeName fullName

        if isFunctionInvokeLeaf n then
            true
        elif isKnownPureOperator n then
            true
        elif index.Exact.Contains(fullName) then
            true
        elif index.Normalized.Contains(n) || index.Exact.Contains(n) then
            true
        else
            index.LastSegment.Contains(lastSegmentKey fullName)

    /// Compose base index with additional PureFile manifests (library embeds, etc.).
    /// Additional method names are unioned into a new index; base is not mutated.
    let compose (baseIndex: Index) (additional: PureFile seq) : Index =
        let extraNames = namesFromPureFiles additional

        if Seq.isEmpty extraNames then
            baseIndex
        else
            let exact = HashSet<string>(baseIndex.Exact, StringComparer.Ordinal)
            let normalized = HashSet<string>(baseIndex.Normalized, StringComparer.Ordinal)
            let lastSeg = HashSet<string>(baseIndex.LastSegment, StringComparer.Ordinal)

            for fn in extraNames do
                if not (String.IsNullOrWhiteSpace fn) then
                    exact.Add(fn) |> ignore
                    let n = normalizeName fn
                    normalized.Add(n) |> ignore
                    lastSeg.Add(lastSegmentKey fn) |> ignore

            {
                Exact = exact
                Normalized = normalized
                LastSegment = lastSeg
            }

    /// Compose foundational index with additional PureFiles.
    let composeWithFoundational (additional: PureFile seq) : Index =
        compose (foundationalIndex ()) additional

    /// Empty pure index (no method names). Used when foundational is disabled.
    let emptyIndex () : Index = buildIndex []

    /// Remove full names (and their normalized / last-segment keys) from an index.
    let without (index: Index) (namesToRemove: string seq) : Index =
        let toRemove = namesToRemove |> Seq.filter (fun s -> not (String.IsNullOrWhiteSpace s)) |> Seq.toList

        if toRemove.IsEmpty then
            index
        else
            let exact = HashSet<string>(index.Exact, StringComparer.Ordinal)
            let normalized = HashSet<string>(index.Normalized, StringComparer.Ordinal)
            let lastSeg = HashSet<string>(index.LastSegment, StringComparer.Ordinal)

            for fn in toRemove do
                exact.Remove(fn) |> ignore
                let n = normalizeName fn
                exact.Remove(n) |> ignore
                normalized.Remove(n) |> ignore
                lastSeg.Remove(lastSegmentKey fn) |> ignore

            {
                Exact = exact
                Normalized = normalized
                LastSegment = lastSeg
            }

    /// Add full names to an index (union).
    let withNames (index: Index) (namesToAdd: string seq) : Index =
        let toAdd = namesToAdd |> Seq.filter (fun s -> not (String.IsNullOrWhiteSpace s))

        if Seq.isEmpty toAdd then
            index
        else
            let exact = HashSet<string>(index.Exact, StringComparer.Ordinal)
            let normalized = HashSet<string>(index.Normalized, StringComparer.Ordinal)
            let lastSeg = HashSet<string>(index.LastSegment, StringComparer.Ordinal)

            for fn in toAdd do
                exact.Add(fn) |> ignore
                let n = normalizeName fn
                normalized.Add(n) |> ignore
                lastSeg.Add(lastSegmentKey fn) |> ignore

            {
                Exact = exact
                Normalized = normalized
                LastSegment = lastSeg
            }

    /// Apply project overrides: remove then add (add wins if a name is in both).
    let applyOverrides (index: Index) (ov: PureOverrides) : Index =
        index
        |> fun i -> without i ov.Remove
        |> fun i -> withNames i ov.Add

    // --- Composition cache (MVID + pure.json content hashes) ---

    let private compositionCache = ConcurrentDictionary<string, Index>(StringComparer.Ordinal)

    /// Build a cache key from assembly MVIDs and per-resource content hashes.
    /// Order of assemblies does not matter (fragments are sorted).
    let makeCompositionCacheKey (parts: (Guid * string seq) seq) : string =
        parts
        |> Seq.map (fun (mvid, hashes) -> PureResourceReader.cacheKeyFragment mvid hashes)
        |> PureResourceReader.compositionCacheKey

    /// Return a cached composed index for `cacheKey`, or compose and store it.
    /// Identical keys always return the same Index instance.
    let getOrComposeCached (cacheKey: string) (baseIndex: Index) (additional: PureFile seq) : Index =
        let key =
            if String.IsNullOrWhiteSpace cacheKey then
                "empty"
            else
                cacheKey

        compositionCache.GetOrAdd(
            key,
            fun _ -> compose baseIndex additional
        )

    /// Clear the composition cache (tests / process recycle).
    let clearCompositionCache () : unit =
        compositionCache.Clear()

    /// Number of entries currently held in the composition cache (tests / diagnostics).
    let compositionCacheCount () : int = compositionCache.Count

    /// Foundational-only lookup (Phase 0/1 default for the live analyser).
    let contains (fullName: string) : bool =
        containsIn (foundationalIndex ()) fullName

    let knownPure: IReadOnlySet<string> = foundationalIndex().Exact
