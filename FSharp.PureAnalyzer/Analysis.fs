namespace FSharp.PureAnalyzer

open System
open System.Collections.Generic
open FSharp.Analyzers.SDK
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text

/// Call-graph construction and transitive purity computation.
module Analysis =

    type CallGraph = Map<string, string list>

    /// A member/function/value that can appear in a call graph (including properties,
    /// whose getters are the usual representation of property access in the TAST).
    let private isCallable (value: FSharpMemberOrFunctionOrValue) =
        value.IsFunction
        || value.IsMember
        || value.IsConstructor
        || value.IsProperty
        || value.IsPropertyGetterMethod
        || value.IsPropertySetterMethod

    /// Does range `outer` fully contain range `inner`?
    let private contains (outer: range) (inner: range) =
        not (Range.equals outer Range.range0)
        && not (Range.equals inner Range.range0)
        && outer.FileName = inner.FileName
        && Range.rangeContainsRange outer inner

    /// Build a call graph purely from symbol uses (works with SDK 0.35.0).
    ///
    /// 1. Collect every callable definition → (DeclarationLocation, name)
    /// 2. Collect every non-definition use of a callable → (useRange, calleeName)
    /// 3. For each use, find the innermost definition whose range contains it;
    ///    that becomes the caller → callee edge.
    let buildCallGraph (_files: FSharpImplementationFileContents seq) (allSymbolUses: FSharpSymbolUse seq) : CallGraph =

        let definitions = ResizeArray<range * string>() // (defRange, defName)
        let uses = ResizeArray<range * string>() // (useRange, calleeName)
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
                    // A use of a callable is treated as a call site.
                    uses.Add(su.Range, name)
            | _ -> ()

        let edges = ResizeArray<string * string>() // (caller, callee)

        for (useRange, calleeName) in uses do
            // Innermost definition that contains this use.
            let mutable best: (range * string) option = None

            for (defRange, defName) in definitions do
                if contains defRange useRange then
                    match best with
                    | None -> best <- Some(defRange, defName)
                    | Some(bestRange, _) ->
                        if Range.rangeContainsRange bestRange defRange then
                            best <- Some(defRange, defName)

            match best with
            | Some(_, callerName) when callerName <> calleeName ->
                // Avoid self-edges from the definition of the name itself.
                edges.Add(callerName, calleeName)
            | _ -> ()

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

    /// Recursive purity check. A function is pure iff it is in the known-pure set
    /// or every callee (transitively) is pure. Cycles are treated as pure to avoid
    /// false positives for mutually recursive functions.
    let isPure (knownPure: IReadOnlySet<string>) (callGraph: CallGraph) (name: string) =
        let rec check visited name =
            if Set.contains name visited then
                true
            elif knownPure.Contains(name) then
                true
            else
                match Map.tryFind name callGraph with
                | Some callees ->
                    let visited = Set.add name visited
                    callees |> List.forall (check visited)
                | None ->
                    // External name that is not in the known-pure whitelist → impure.
                    false

        check Set.empty name

    /// Returns the set of all functions in the call graph that are not transitively pure.
    let findNonPure (knownPure: IReadOnlySet<string>) (callGraph: CallGraph) : Set<string> =
        callGraph
        |> Map.toSeq
        |> Seq.map fst
        |> Seq.filter (isPure knownPure callGraph >> not)
        |> Set.ofSeq
