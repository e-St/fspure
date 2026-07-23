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

    let private isCallable (value: FSharpMemberOrFunctionOrValue) =
        value.IsFunction || value.IsMember || value.IsConstructor

    /// Build a call graph from the typed AST of one or more implementation files.
    /// Each top-level declaration becomes a node; every direct call inside its body
    /// becomes an edge to the callee's full name.
    let buildCallGraph (files: FSharpImplementationFileContents seq) : CallGraph =
        let declarations = HashSet<string>(StringComparer.Ordinal)
        let edges = ResizeArray<string * string>()
        let mutable currentFunction: string option = None

        let setCurrent value =
            if isCallable value then
                let name = Name.fullNameOfMember value
                declarations.Add(name) |> ignore
                currentFunction <- Some name
            else
                currentFunction <- None

        let addCallee (callee: FSharpMemberOrFunctionOrValue) =
            if not (isCallable callee) then
                ()
            else
                match currentFunction with
                | None -> ()
                | Some caller ->
                    let calleeName = Name.fullNameOfMember callee
                    edges.Add(caller, calleeName)

        let collector =
            { new TypedTreeCollectorBase() with
                override _.WalkMemberOrFunctionOrValue value _ _ = setCurrent value

                override _.WalkCall _ memberOrFunc _ _ _ _ = addCallee memberOrFunc

                override _.WalkApplication funcExpr _ _ =
                    match funcExpr with
                    | Value value -> addCallee value
                    | _ -> ()
            }

        for file in files do
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
                | None -> false

        check Set.empty name

    /// Returns the set of all functions in the call graph that are not transitively pure.
    let findNonPure (knownPure: IReadOnlySet<string>) (callGraph: CallGraph) : Set<string> =
        callGraph
        |> Map.toSeq
        |> Seq.map fst
        |> Seq.filter (isPure knownPure callGraph >> not)
        |> Set.ofSeq
