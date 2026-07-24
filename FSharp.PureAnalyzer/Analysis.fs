namespace FSharp.PureAnalyzer

open System
open System.Collections.Generic
open FSharp.Analyzers.SDK
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text

module Analysis =

    type CallGraph = Map<string, string list>

    let private isCallable (value: FSharpMemberOrFunctionOrValue) =
        value.IsFunction
        || value.IsMember
        || value.IsConstructor
        || value.IsProperty
        || value.IsPropertyGetterMethod
        || value.IsPropertySetterMethod

    let private contains (outer: range) (inner: range) =
        not (Range.equals outer Range.range0)
        && not (Range.equals inner Range.range0)
        && Range.rangeContainsRange outer inner

    /// Builds a call graph from a collection of symbol uses.
    /// Works for both full-project and single-file scenarios.
    ///
    /// Strategy:
    /// 1. Collect every callable definition (IsFromDefinition) together with its
    ///    DeclarationLocation range.
    /// 2. For every use, find the tightest enclosing definition by range containment.
    /// 3. If no edges are discovered (common when DeclarationLocation is missing or
    ///    ranges do not nest cleanly) fall back to a complete bipartite connection
    ///    between every definition and every use. This is conservative but guarantees
    ///    that transitive impurity is still detected for small files.
    let buildCallGraph (_files: FSharpImplementationFileContents seq) (allSymbolUses: FSharpSymbolUse seq) : CallGraph =

        let definitions = ResizeArray<range * string>()
        let uses = ResizeArray<range * string>()
        let declarationSet = HashSet<string>(StringComparer.Ordinal)

        for su in allSymbolUses do
            match su.Symbol with
            | :? FSharpMemberOrFunctionOrValue as v when isCallable v ->
                let name = Name.fullNameOfMember v

                if su.IsFromDefinition then
                    let fullRange = v.DeclarationLocation

                    if not (Range.equals fullRange Range.range0) then
                        definitions.Add(fullRange, name)
                        declarationSet.Add(name) |> ignore
                    else
                        // Still record the definition even if the range is missing
                        // so that the fallback path can see it.
                        declarationSet.Add(name) |> ignore
                else
                    uses.Add(su.Range, name)
            | _ -> ()

        let edges = ResizeArray<string * string>()

        for (useRange, calleeName) in uses do
            let mutable best: (range * string) option = None

            for (defRange, defName) in definitions do
                if contains defRange useRange then
                    match best with
                    | None -> best <- Some(defRange, defName)
                    | Some(bestRange, _) ->
                        // Prefer the tightest (innermost) enclosing definition
                        if Range.rangeContainsRange bestRange defRange then
                            best <- Some(defRange, defName)

            match best with
            | Some(_, callerName) when callerName <> calleeName -> edges.Add(callerName, calleeName)
            | _ -> ()

        // Fallback when ranges do not match: attribute every use to every definition.
        // This is deliberately over-approximating so that impurity still propagates
        // in the small single-file case that FSAC usually gives us.
        if edges.Count = 0 && declarationSet.Count > 0 && uses.Count > 0 then
            for defName in declarationSet do
                for (_, calleeName) in uses do
                    if defName <> calleeName then
                        edges.Add(defName, calleeName)

        let edgeMap =
            edges
            |> Seq.groupBy fst
            |> Seq.map (fun (caller, pairs) ->
                let callees = pairs |> Seq.map snd |> Seq.distinct |> Seq.toList
                caller, callees)
            |> Map.ofSeq

        declarationSet
        |> Seq.map (fun name -> name, Map.tryFind name edgeMap |> Option.defaultValue [])
        |> Map.ofSeq

    let isPure (knownPure: IReadOnlySet<string>) (callGraph: CallGraph) (name: string) =
        let rec check visited name =
            if Set.contains name visited then
                true // cycle – treat as pure to avoid infinite recursion
            elif knownPure.Contains(name) then
                true
            else
                match Map.tryFind name callGraph with
                | Some callees ->
                    let visited = Set.add name visited
                    callees |> List.forall (check visited)
                | None ->
                    // Unknown external symbol that is not in the pure set → impure
                    false

        check Set.empty name

    let findNonPure (knownPure: IReadOnlySet<string>) (callGraph: CallGraph) : Set<string> =
        callGraph
        |> Map.toSeq
        |> Seq.map fst
        |> Seq.filter (isPure knownPure callGraph >> not)
        |> Set.ofSeq
