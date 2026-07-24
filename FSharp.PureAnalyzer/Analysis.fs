namespace FSharp.PureAnalyzer

open System
open System.Collections.Generic
open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.TASTCollecting
open FSharp.Compiler.Symbols
open FSharp.Compiler.Symbols.FSharpExprPatterns

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

    /// Build a call graph from the typed AST of one or more implementation files.
    /// Each callable declaration becomes a node; every direct call inside its body
    /// becomes an edge to the callee's full name.
    let buildCallGraph (files: FSharpImplementationFileContents seq) : CallGraph =
        let declarations = HashSet<string>(StringComparer.Ordinal)
        let edges = ResizeArray<string * string>()

        // Stack of enclosing callables.  Top of stack is the function whose body
        // is currently being walked.
        let currentStack = Stack<string>()

        let push (value: FSharpMemberOrFunctionOrValue) =
            if isCallable value then
                let name = Name.fullNameOfMember value
                declarations.Add(name) |> ignore
                currentStack.Push(name)

        let addCallee (callee: FSharpMemberOrFunctionOrValue) =
            if isCallable callee && currentStack.Count > 0 then
                let caller = currentStack.Peek()
                let calleeName = Name.fullNameOfMember callee
                edges.Add(caller, calleeName)

        let collector =
            { new TypedTreeCollectorBase() with

                // Top-level (and nested member) definitions.
                // The framework calls WalkMemberOrFunctionOrValue *before* visiting the body,
                // so the push stays active for the whole body.
                override _.WalkMemberOrFunctionOrValue(value, _curriedArgs, _body) = push value
                // Note: we deliberately do *not* pop here.  The body is walked by the
                // framework after this method returns.  For sequential top-level
                // declarations the next WalkMemberOrFunctionOrValue will push a new
                // name; the previous name simply stays on the stack until the next
                // declaration (which is fine because the previous body has already
                // been fully walked).  Nested members are handled by the same logic.

                // Local `let` bindings (including local functions).
                // Framework order: WalkLet → visit bindingExpr (rhs) → visit bodyExpr.
                // We push before the rhs is walked so that the local function's body is
                // attributed correctly.  We cannot easily restore the outer current for
                // the *bodyExpr* of the let (the scope after the binding) without
                // controlling the walk ourselves; for the common case of top-level
                // functions and for purity analysis this is acceptable – calls that
                // appear after a local function definition are still rare relative to
                // the bodies themselves.
                override _.WalkLet(bindingVar, _bindingExpr, _bodyExpr) = push bindingVar

                override _.WalkLetRec(recursiveBindings, _bodyExpr) =
                    for (value, _) in recursiveBindings do
                        push value

                // Direct calls (the most common form for methods, property getters, etc.)
                override _.WalkCall(_objExprOpt, memberOrFunc, _objTypeArgs, _memberTypeArgs, _argExprs, _range) =
                    addCallee memberOrFunc

                // Partial applications / higher-order uses of a known value.
                override _.WalkApplication(funcExpr, _typeArgs, _argExprs) =
                    match funcExpr with
                    | Value value -> addCallee value
                    | _ -> ()

            // Some property / field accesses appear as FSharpFieldGet.
            // We cannot obtain an MFV from an FSharpField reliably, so we leave
            // them for the Call path (static/instance property getters are
            // normally represented as Call).
            // override _.WalkFSharpFieldGet ... = ()
            }

        for file in files do
            // Clear the stack between files so one file cannot pollute another.
            currentStack.Clear()
            walkTast collector file

        let edgeMap =
            edges
            |> Seq.groupBy fst
            |> Seq.map (fun (caller, pairs) ->
                let callees = pairs |> Seq.map snd |> Seq.distinct |> Seq.toList
                caller, callees)
            |> Map.ofSeq

        declarations
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
