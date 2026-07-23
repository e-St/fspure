namespace FSharp.PureAnalyzer

open System
open System.Collections.Generic

/// Recursive purity fixed-point over a call graph (List A core).
module PurityEngine =

    /// Build callee map and local-impurity set from analyzed methods.
    let buildGraphs (methods: AnalyzedMethod list) : Map<string, string list> * Set<string> * Map<string, AnalyzedMethod> =
        let byName =
            methods
            |> List.groupBy _.FullName
            |> List.map (fun (name, group) ->
                // Prefer a body-bearing definition when duplicates exist across assemblies.
                let chosen =
                    group
                    |> List.sortByDescending (fun m -> (if m.HasBody then 1 else 0), (if m.IsPublic then 1 else 0))
                    |> List.head

                name, chosen)
            |> Map.ofList

        let callGraph = byName |> Map.map (fun _ m -> m.Callees)

        let locallyImpure =
            byName
            |> Map.toList
            |> List.choose (fun (name, m) -> if m.HasLocalImpurity then Some name else None)
            |> Set.ofList

        callGraph, locallyImpure, byName

    /// Compute the greatest fixed-point set of pure methods.
    /// A method is pure iff:
    ///   1. it is not locally impure, and
    ///   2. every known callee is pure (unknown external callees are treated as impure
    ///      unless they are known pure leaves).
    let computePureSet (methods: AnalyzedMethod list) : Set<string> * Map<string, AnalyzedMethod> =
        let callGraph, locallyImpure, byName = buildGraphs methods
        let allNames = byName |> Map.toList |> List.map fst |> Set.ofList

        // Seed: everything not locally impure starts as a purity candidate.
        // Known pure leaves are always candidates even if missing from assemblies.
        let mutable pureSet =
            allNames
            |> Set.filter (fun n -> not (Set.contains n locallyImpure))
            |> Set.union ImpurityRules.knownPureLeaves

        // Unknown callees (outside analyzed assemblies) are impure unless known leaves.
        let isCalleeAcceptable (name: string) =
            ImpurityRules.isKnownPureLeaf name || Set.contains name pureSet

        let mutable changed = true
        let mutable iterations = 0
        let maxIterations = 10_000

        while changed && iterations < maxIterations do
            changed <- false
            iterations <- iterations + 1
            let snapshot = pureSet

            for name in snapshot do
                if not (ImpurityRules.isKnownPureLeaf name) then
                    match Map.tryFind name callGraph with
                    | None ->
                        // Not in analyzed set – keep only if known leaf (already handled).
                        if not (Set.contains name allNames) && not (ImpurityRules.isKnownPureLeaf name) then
                            pureSet <- Set.remove name pureSet
                            changed <- true
                    | Some callees ->
                        let ok =
                            not (Set.contains name locallyImpure)
                            && callees |> List.forall isCalleeAcceptable

                        if not ok then
                            pureSet <- Set.remove name pureSet
                            changed <- true

        // Restrict output to methods we actually analyzed (plus keep leaves that appear).
        let result =
            pureSet
            |> Set.filter (fun n -> Set.contains n allNames || ImpurityRules.isKnownPureLeaf n)

        result, byName

    let private isExportableName (name: string) =
        not (
            String.IsNullOrWhiteSpace name
            || name.StartsWith("<", StringComparison.Ordinal)
            || name.StartsWith("-", StringComparison.Ordinal)
            || name.Contains("<>", StringComparison.Ordinal)
            || name.Contains("@", StringComparison.Ordinal)
            || name.Contains("|", StringComparison.Ordinal)
        )

    /// Convert a pure-name set into PureMethod records (origin = automatic).
    let toPureMethods (pureSet: Set<string>) (byName: Map<string, AnalyzedMethod>) (publicOnly: bool) : PureMethod list =
        pureSet
        |> Set.toList
        |> List.choose (fun name ->
            if not (isExportableName name) then
                None
            else
                match Map.tryFind name byName with
                | Some m when publicOnly && not m.IsPublic -> None
                | Some _
                | None -> Some { FullName = name; Origin = Automatic })
        |> List.sortBy _.FullName

    /// Design-doc recursive checker (used for validation / tests).
    let isPure (knownPure: Set<string>) (callGraph: Map<string, string list>) (name: string) : bool =
        let rec check (visited: Set<string>) (name: string) =
            if Set.contains name visited then
                true
            elif not (Set.contains name knownPure) then
                false
            else
                let visited = Set.add name visited

                match Map.tryFind name callGraph with
                | Some callees -> callees |> List.forall (check visited)
                | None -> true

        check Set.empty name

    /// High-level List A pipeline.
    let buildListA
        (methods: AnalyzedMethod list)
        (packageId: string)
        (packageVersion: string)
        (publicOnly: bool)
        : PureFile * Set<string> * Map<string, AnalyzedMethod> =
        let pureSet, byName = computePureSet methods
        let pureMethods = toPureMethods pureSet byName publicOnly

        let file =
            {
                SchemaVersion = Constants.SchemaVersion
                PackageId = packageId
                PackageVersion = packageVersion
                GeneratedAt = DateTimeOffset.UtcNow
                Generator = Constants.Generator
                PureMethods = pureMethods
            }

        file, pureSet, byName
