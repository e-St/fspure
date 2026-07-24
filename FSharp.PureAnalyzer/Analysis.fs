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

        // Stack of enclosing callables. Top of stack is the function whose body
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
                // In SDK 0.35 the signature is only the value; the framework walks
                // the body *after* this method returns, so the pushed name stays
                // active for the whole body.
                override _.WalkMemberOrFunctionOrValue value = push value

                // Local `let` bindings (including local functions).
                // Framework order: WalkLet → visit bindingExpr (rhs) → visit bodyExpr.
                // We push so that the local function's body is attributed correctly.
                // Calls that appear in the scope *after* the local binding will also
                // see the local name; this is a known limitation of the visitor
                // (enter without a matching exit). For typical top-level code it
                // does not matter.
                override _.WalkLet bindingVar _bindingExpr _bodyExpr = push bindingVar

                override _.WalkLetRec recursiveBindings _bodyExpr =
                    for (value, _) in recursiveBindings do
                        push value

                // Direct calls (methods, property getters, etc.)
                override _.WalkCall _objExprOpt memberOrFunc _objTypeArgs _memberTypeArgs _argExprs _range =
                    addCallee memberOrFunc

                // Partial applications / higher-order uses of a known value.
                override _.WalkApplication funcExpr _typeArgs _argExprs =
                    match funcExpr with
                    | Value value -> addCallee value
                    | _ -> ()
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
