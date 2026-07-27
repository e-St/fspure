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

    /// Stable key for a mutable binding so ValueSet can match the declaring let.
    let private mutableKey (v: FSharpMemberOrFunctionOrValue) =
        let r = v.DeclarationLocation
        sprintf "%s@%s:%d:%d" v.LogicalName r.FileName r.StartLine r.StartColumn

    /// Builds a call graph by walking the TypedTree.
    ///
    /// Purity rules:
    /// - Only actual *calls* / object construction create callee edges
    ///   (a bare function *reference* is not a call).
    /// - Nested let/lambda do not push a new caller frame.
    /// - `<-` to a mutable declared inside the current function is allowed (still pure).
    /// - `<-` to anything else (module mutable, outer scope, etc.) marks the function impure.
    let buildCallGraph (files: FSharpImplementationFileContents seq) (_allSymbolUses: FSharpSymbolUse seq) : CallGraph * Set<string> =

        let edges = Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        let definitions = HashSet<string>(StringComparer.Ordinal)
        /// Function names that perform non-local mutation via `<-`
        let nonLocalMutation = HashSet<string>(StringComparer.Ordinal)
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

        let markNonLocalMutation () =
            if current.Count > 0 then
                nonLocalMutation.Add(current.Peek()) |> ignore

        /// Walk an expression under the current function, tracking mutables local to this frame.
        let rec visitExpr (localMutables: HashSet<string>) (e: FSharpExpr) =
            match e with
            | Call(objExprOpt, memberOrFunc, _, _, argExprs) ->
                recordCall memberOrFunc
                objExprOpt |> Option.iter (visitExpr localMutables)
                argExprs |> List.iter (visitExpr localMutables)

            | Value _ ->
                // Bare reference — not a call, not a mutation.
                ()

            | NewObject(objType, _, argExprs) ->
                recordCall objType
                argExprs |> List.iter (visitExpr localMutables)

            | Let((bindingVar, bindingExpr, _), bodyExpr) ->
                if bindingVar.IsMutable then
                    localMutables.Add(mutableKey bindingVar) |> ignore

                visitExpr localMutables bindingExpr
                visitExpr localMutables bodyExpr

            | LetRec(recursiveBindings, bodyExpr) ->
                for (mfv, _, _) in recursiveBindings do
                    if mfv.IsMutable then
                        localMutables.Add(mutableKey mfv) |> ignore

                for (_, expr, _) in recursiveBindings do
                    visitExpr localMutables expr

                visitExpr localMutables bodyExpr

            | Lambda(_, bodyExpr) ->
                // Nested lambda: still attribute effects to the enclosing member.
                // Locals declared inside the lambda are still "local" to this function.
                visitExpr localMutables bodyExpr

            | ValueSet(valToSet, valueExpr) ->
                // `x <- e`
                if not (localMutables.Contains(mutableKey valToSet)) then
                    markNonLocalMutation ()

                visitExpr localMutables valueExpr

            | AddressSet(lvalueExpr, rvalueExpr) ->
                // byref store — treat as non-local unless we can prove otherwise
                markNonLocalMutation ()
                visitExpr localMutables lvalueExpr
                visitExpr localMutables rvalueExpr

            | FSharpFieldSet(objOpt, _, _, valueExpr) ->
                // Field write: conservative — non-local mutation
                // (local struct field updates are uncommon in pure style)
                markNonLocalMutation ()
                objOpt |> Option.iter (visitExpr localMutables)
                visitExpr localMutables valueExpr

            | ILFieldSet(objOpt, _, _, valueExpr) ->
                markNonLocalMutation ()
                objOpt |> Option.iter (visitExpr localMutables)
                visitExpr localMutables valueExpr

            | Application(funcExpr, _, argExprs) ->
                visitExpr localMutables funcExpr
                argExprs |> List.iter (visitExpr localMutables)

            | IfThenElse(g, t, f) ->
                visitExpr localMutables g
                visitExpr localMutables t
                visitExpr localMutables f

            | Sequential(e1, e2) ->
                visitExpr localMutables e1
                visitExpr localMutables e2

            | TryFinally(body, fin, _, _) ->
                visitExpr localMutables body
                visitExpr localMutables fin

            | TryWith(body, _, _, _, catchExpr, _, _) ->
                visitExpr localMutables body
                visitExpr localMutables catchExpr

            | WhileLoop(guard, body, _) ->
                visitExpr localMutables guard
                visitExpr localMutables body

            | FastIntegerForLoop(start, limit, consume, _, _, _) ->
                visitExpr localMutables start
                visitExpr localMutables limit
                visitExpr localMutables consume

            | AddressOf e1 -> visitExpr localMutables e1
            | Coerce(_, e1) -> visitExpr localMutables e1
            | Quote e1 -> visitExpr localMutables e1
            | TypeLambda(_, e1) -> visitExpr localMutables e1
            | TypeTest(_, e1) -> visitExpr localMutables e1
            | TupleGet(_, _, e1) -> visitExpr localMutables e1
            | UnionCaseGet(e1, _, _, _) -> visitExpr localMutables e1
            | UnionCaseTest(e1, _, _) -> visitExpr localMutables e1
            | UnionCaseTag(e1, _) -> visitExpr localMutables e1
            | UnionCaseSet(e1, _, _, _, e2) ->
                markNonLocalMutation ()
                visitExpr localMutables e1
                visitExpr localMutables e2
            | FSharpFieldGet(objOpt, _, _) -> objOpt |> Option.iter (visitExpr localMutables)
            | ILFieldGet(objOpt, _, _) -> objOpt |> Option.iter (visitExpr localMutables)
            | NewArray(_, args) -> args |> List.iter (visitExpr localMutables)
            | NewRecord(_, args) -> args |> List.iter (visitExpr localMutables)
            | NewTuple(_, args) -> args |> List.iter (visitExpr localMutables)
            | NewUnionCase(_, _, args) -> args |> List.iter (visitExpr localMutables)
            | NewDelegate(_, body) -> visitExpr localMutables body
            | DecisionTree(decision, targets) ->
                visitExpr localMutables decision
                targets |> List.iter (snd >> visitExpr localMutables)
            | DecisionTreeSuccess(_, exprs) -> exprs |> List.iter (visitExpr localMutables)
            | ObjectExpr(_, baseCall, _, _) -> visitExpr localMutables baseCall
            | TraitCall(_, _, _, _, _, args) -> args |> List.iter (visitExpr localMutables)
            | ILAsm(_, _, args) -> args |> List.iter (visitExpr localMutables)
            | _ -> ()

        let rec visitDeclaration (d: FSharpImplementationFileDeclaration) =
            match d with
            | FSharpImplementationFileDeclaration.Entity(_, decls) ->
                decls |> List.iter visitDeclaration

            | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(v, _vs, body) ->
                let name = Name.fullNameOfMember v
                definitions.Add(name) |> ignore

                if isCallable v then
                    current.Push(name)
                    let localMutables = HashSet<string>(StringComparer.Ordinal)
                    visitExpr localMutables body
                    current.Pop() |> ignore
                else
                    visitExpr (HashSet<string>(StringComparer.Ordinal)) body

            | FSharpImplementationFileDeclaration.InitAction(expr) ->
                visitExpr (HashSet<string>(StringComparer.Ordinal)) expr

        for file in files do
            file.Declarations |> List.iter visitDeclaration

        for name in definitions do
            if not (edges.ContainsKey(name)) then
                edges.[name] <- HashSet<string>(StringComparer.Ordinal)

        let callGraph =
            edges
            |> Seq.map (fun (KeyValue(k, v)) -> k, v |> Seq.toList)
            |> Map.ofSeq

        let mutationSet = nonLocalMutation |> Set.ofSeq
        callGraph, mutationSet

    /// True when `name` is pure given the call graph and non-local mutation set.
    /// Uses PureSet.contains so FCS vs IL name differences are tolerated.
    let isPure
        (callGraph: CallGraph)
        (nonLocalMutation: Set<string>)
        (name: string)
        =
        let rec check visited name =
            if Set.contains name visited then
                true
            elif Set.contains name nonLocalMutation then
                false
            elif PureSet.contains name then
                true
            else
                match Map.tryFind name callGraph with
                | Some callees ->
                    let visited = Set.add name visited
                    callees |> List.forall (check visited)
                | None ->
                    false

        check Set.empty name

    let findNonPure
        (callGraph: CallGraph)
        (nonLocalMutation: Set<string>)
        : Set<string> =
        callGraph
        |> Map.toSeq
        |> Seq.map fst
        |> Seq.filter (fun name -> not (isPure callGraph nonLocalMutation name))
        |> Set.ofSeq
