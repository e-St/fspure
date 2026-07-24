namespace FSharp.PureAnalyzer

open System
open System.Collections.Generic
open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.TASTCollecting
open FSharp.Compiler.Symbols
open FSharp.Compiler.Symbols.FSharpExprPatterns
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
        outer.FileName = inner.FileName && Range.rangeContainsRange outer inner

    /// Build a call graph from the typed AST + the project's symbol uses.
    /// Works with FSharp.Analyzers.SDK 0.35.0 (no WalkMemberOrFunctionOrValue required).
    ///
    /// Strategy:
    /// 1. Walk the TAST and record every call site as (callRange, calleeName).
    /// 2. From symbol uses collect every callable definition as (defRange, defName).
    /// 3. For each call find the innermost definition whose range contains the call;
    ///    that definition is the caller.
    let buildCallGraph (files: FSharpImplementationFileContents seq) (allSymbolUses: FSharpSymbolUse seq) : CallGraph =

        // ----- 1. Collect call sites from the TAST ---------------------------------
        let calls = ResizeArray<range * string>() // (callRange, calleeName)

        let addCall (range: range) (callee: FSharpMemberOrFunctionOrValue) =
            if isCallable callee then
                let name = Name.fullNameOfMember callee
                calls.Add(range, name)

        let collector =
            { new TypedTreeCollectorBase() with
                override _.WalkCall _objExprOpt memberOrFunc _objTypeArgs _memberTypeArgs _argExprs range =
                    addCall range memberOrFunc

                override _.WalkApplication funcExpr _typeArgs _argExprs =
                    // Application nodes do not carry an explicit range in the walker
                    // signature; we approximate with the range of the function expression
                    // when it is a simple Value.  Most real calls go through WalkCall.
                    match funcExpr with
                    | Value value ->
                        // Value expressions expose their range via the symbol use later;
                        // here we can only record the callee.  We use a dummy range that
                        // will not match any definition, so pure Applications without a
                        // corresponding Call are ignored.  In practice the important
                        // cases (method / property calls) hit WalkCall.
                        ()
                    | _ -> ()
            }

        for file in files do
            walkTast collector file

        // ----- 2. Collect definitions from symbol uses -----------------------------
        let definitions = ResizeArray<range * string>() // (defRange, defName)

        for su in allSymbolUses do
            if su.IsFromDefinition then
                match su.Symbol with
                | :? FSharpMemberOrFunctionOrValue as v when isCallable v ->
                    let name = Name.fullNameOfMember v
                    definitions.Add(su.Range, name)
                | _ -> ()

        // ----- 3. Match calls to their innermost enclosing definition --------------
        let edges = ResizeArray<string * string>() // (caller, callee)
        let declarationSet = HashSet<string>(StringComparer.Ordinal)

        for (defRange, defName) in definitions do
            declarationSet.Add(defName) |> ignore

        for (callRange, calleeName) in calls do
            // Find the tightest (smallest) definition that contains the call.
            let mutable best: (range * string) option = None

            for (defRange, defName) in definitions do
                if contains defRange callRange then
                    match best with
                    | None -> best <- Some(defRange, defName)
                    | Some(bestRange, _) ->
                        // Prefer the smaller range (more nested).
                        if Range.rangeContainsRange bestRange defRange then
                            best <- Some(defRange, defName)

            match best with
            | Some(_, callerName) -> edges.Add(callerName, calleeName)
            | None -> () // call not inside any known definition (e.g. top-level init)

        // ----- 4. Build the final map ----------------------------------------------
        let edgeMap =
            edges
            |> Seq.groupBy fst
            |> Seq.map (fun (caller, pairs) ->
                let callees = pairs |> Seq.map snd |> Seq.distinct |> Seq.toList
                caller, callees)
            |> Map.ofSeq

        declarationSet
        |> Seq.map (fun name -> name, (Map.tryFind name edgeMap |> Option.defaultValue []))
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
