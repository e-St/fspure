namespace FSharp.PureAnalyzer

open System
open System.Collections.Generic
open FSharp.Analyzers.SDK
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Symbols
open FSharp.Compiler.Symbols.FSharpExprPatterns
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

    /// Builds a call graph by walking the TypedTree.
    /// Maintains an explicit stack of the current enclosing callable so that
    /// every Call / Value / NewObject is attributed to the correct caller.
    let buildCallGraph (files: FSharpImplementationFileContents seq) (_allSymbolUses: FSharpSymbolUse seq) : CallGraph =

        let edges = Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        let definitions = HashSet<string>(StringComparer.Ordinal)
        let current = Stack<string>()

        let addEdge (caller: string) (callee: string) =
            if caller <> callee then
                let set =
                    match edges.TryGetValue(caller) with
                    | true, s -> s
                    | false, _ ->
                        let s = HashSet<string>(StringComparer.Ordinal)
                        edges.[caller] <- s
                        s

                set.Add(callee) |> ignore

        let recordCall (callee: FSharpMemberOrFunctionOrValue) =
            if isCallable callee && current.Count > 0 then
                let caller = current.Peek()
                let calleeName = Name.fullNameOfMember callee
                addEdge caller calleeName

        let rec visitExpr (e: FSharpExpr) =
            match e with
            | Call(objExprOpt, memberOrFunc, _, _, argExprs) ->
                recordCall memberOrFunc
                objExprOpt |> Option.iter visitExpr
                argExprs |> List.iter visitExpr

            | Value valueToGet -> recordCall valueToGet

            | NewObject(objType, _, argExprs) ->
                recordCall objType
                argExprs |> List.iter visitExpr

            | Let((bindingVar, bindingExpr, _), bodyExpr) ->
                let name = Name.fullNameOfMember bindingVar
                definitions.Add(name) |> ignore

                if isCallable bindingVar then
                    current.Push(name)
                    visitExpr bindingExpr
                    current.Pop() |> ignore
                else
                    visitExpr bindingExpr

                visitExpr bodyExpr

            | LetRec(recursiveBindings, bodyExpr) ->
                // Register all recursive names first
                for (mfv, _, _) in recursiveBindings do
                    let name = Name.fullNameOfMember mfv
                    definitions.Add(name) |> ignore

                for (mfv, expr, _) in recursiveBindings do
                    let name = Name.fullNameOfMember mfv

                    if isCallable mfv then
                        current.Push(name)
                        visitExpr expr
                        current.Pop() |> ignore
                    else
                        visitExpr expr

                visitExpr bodyExpr

            | Lambda(lambdaVar, bodyExpr) ->
                let name = Name.fullNameOfMember lambdaVar
                definitions.Add(name) |> ignore

                if isCallable lambdaVar then
                    current.Push(name)
                    visitExpr bodyExpr
                    current.Pop() |> ignore
                else
                    visitExpr bodyExpr

            | Application(funcExpr, _, argExprs) ->
                visitExpr funcExpr
                argExprs |> List.iter visitExpr

            | IfThenElse(g, t, f) ->
                visitExpr g
                visitExpr t
                visitExpr f

            | Sequential(e1, e2) ->
                visitExpr e1
                visitExpr e2

            | TryFinally(body, fin, _, _) ->
                visitExpr body
                visitExpr fin

            | TryWith(body, _, _, _, catchExpr, _, _) ->
                visitExpr body
                visitExpr catchExpr

            | WhileLoop(guard, body, _) ->
                visitExpr guard
                visitExpr body

            | FastIntegerForLoop(start, limit, consume, _, _, _) ->
                visitExpr start
                visitExpr limit
                visitExpr consume

            | AddressOf e1 -> visitExpr e1
            | AddressSet(e1, e2) ->
                visitExpr e1
                visitExpr e2
            | Coerce(_, e1) -> visitExpr e1
            | Quote e1 -> visitExpr e1
            | TypeLambda(_, e1) -> visitExpr e1
            | TypeTest(_, e1) -> visitExpr e1
            | TupleGet(_, _, e1) -> visitExpr e1
            | UnionCaseGet(e1, _, _, _) -> visitExpr e1
            | UnionCaseTest(e1, _, _) -> visitExpr e1
            | UnionCaseTag(e1, _) -> visitExpr e1
            | UnionCaseSet(e1, _, _, _, e2) ->
                visitExpr e1
                visitExpr e2
            | ValueSet(_, e1) -> visitExpr e1
            | FSharpFieldGet(objOpt, _, _) -> objOpt |> Option.iter visitExpr
            | FSharpFieldSet(objOpt, _, _, e1) ->
                objOpt |> Option.iter visitExpr
                visitExpr e1
            | ILFieldGet(objOpt, _, _) -> objOpt |> Option.iter visitExpr
            | ILFieldSet(objOpt, _, _, e1) ->
                objOpt |> Option.iter visitExpr
                visitExpr e1
            | NewArray(_, args) -> args |> List.iter visitExpr
            | NewRecord(_, args) -> args |> List.iter visitExpr
            | NewTuple(_, args) -> args |> List.iter visitExpr
            | NewUnionCase(_, _, args) -> args |> List.iter visitExpr
            | NewDelegate(_, body) -> visitExpr body
            | DecisionTree(decision, targets) ->
                visitExpr decision
                targets |> List.iter (snd >> visitExpr)
            | DecisionTreeSuccess(_, exprs) -> exprs |> List.iter visitExpr
            | ObjectExpr(_, baseCall, overrides, interfaces) ->
                visitExpr baseCall
                // overrides / interfaces contain expressions we ignore for purity edges
                // (method bodies of object expressions are a future refinement)
                ()
            | TraitCall(_, _, _, _, _, args) -> args |> List.iter visitExpr
            | ILAsm(_, _, args) -> args |> List.iter visitExpr
            | _ -> ()

        let rec visitDeclaration (d: FSharpImplementationFileDeclaration) =
            match d with
            | FSharpImplementationFileDeclaration.Entity(_, decls) -> decls |> List.iter visitDeclaration

            | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(v, _vs, body) ->
                let name = Name.fullNameOfMember v
                definitions.Add(name) |> ignore

                if isCallable v then
                    current.Push(name)
                    visitExpr body
                    current.Pop() |> ignore
                else
                    visitExpr body

            | FSharpImplementationFileDeclaration.InitAction(expr) -> visitExpr expr

        for file in files do
            file.Declarations |> List.iter visitDeclaration

        // Every definition appears in the final graph (empty list = pure by default)
        for name in definitions do
            if not (edges.ContainsKey(name)) then
                edges.[name] <- HashSet<string>(StringComparer.Ordinal)

        edges |> Seq.map (fun (KeyValue(k, v)) -> k, v |> Seq.toList) |> Map.ofSeq

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
                | None -> false // external & not in pure set → impure

        check Set.empty name

    let findNonPure (knownPure: IReadOnlySet<string>) (callGraph: CallGraph) : Set<string> =
        callGraph
        |> Map.toSeq
        |> Seq.map fst
        |> Seq.filter (isPure knownPure callGraph >> not)
        |> Set.ofSeq
