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

    /// Stable key for a mutable / ref binding so assignments can match the declaring let.
    let private bindingKey (v: FSharpMemberOrFunctionOrValue) =
        let r = v.DeclarationLocation
        sprintf "%s@%s:%d:%d" v.LogicalName r.FileName r.StartLine r.StartColumn

    let private fullName (m: FSharpMemberOrFunctionOrValue) = Name.fullNameOfMember m

    /// `ref x` / Operators.ref — allocates a local ref cell when bound with let.
    let private isRefAlloc (m: FSharpMemberOrFunctionOrValue) =
        let ln = m.LogicalName
        let n = fullName m

        ln = "ref"
        || ln = "Ref"
        || n.EndsWith(".ref", StringComparison.Ordinal)
        || n.EndsWith(".Ref", StringComparison.Ordinal)
        || n.IndexOf("Operators.ref", StringComparison.OrdinalIgnoreCase) >= 0

    /// `r := e`  (Operators.op_ColonEquals)
    let private isColonEquals (m: FSharpMemberOrFunctionOrValue) =
        let ln = m.LogicalName
        let n = fullName m

        ln = "op_ColonEquals" || n.EndsWith(".op_ColonEquals", StringComparison.Ordinal)

    /// Builds a call graph by walking the TypedTree.
    ///
    /// Mutation rules (same for `<-` and `:=`):
    /// - Assignment to a mutable / ref allocated in the current function → still pure
    /// - Assignment to anything else → function is non-locally mutating (impure)
    /// - `op_ColonEquals` is not treated as a normal callee edge (would always look impure)
    let buildCallGraph
        (files: FSharpImplementationFileContents seq)
        (_allSymbolUses: FSharpSymbolUse seq)
        : CallGraph * Set<string> =

        let edges = Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        let definitions = HashSet<string>(StringComparer.Ordinal)
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
                addEdge caller (fullName callee)

        let markNonLocalMutation () =
            if current.Count > 0 then
                nonLocalMutation.Add(current.Peek()) |> ignore

        /// localMutables: `let mutable` keys and local `let r = ref ...` keys
        let rec visitExpr (localMutables: HashSet<string>) (e: FSharpExpr) =
            match e with
            | Call(objExprOpt, memberOrFunc, _, _, argExprs) ->
                if isColonEquals memberOrFunc then
                    // r := value  — same rule as ValueSet / `<-`
                    match argExprs with
                    | Value v :: rest when localMutables.Contains(bindingKey v) ->
                        rest |> List.iter (visitExpr localMutables)
                    | Value _ :: rest ->
                        markNonLocalMutation ()
                        rest |> List.iter (visitExpr localMutables)
                    | _ ->
                        markNonLocalMutation ()
                        argExprs |> List.iter (visitExpr localMutables)

                    objExprOpt |> Option.iter (visitExpr localMutables)
                else
                    recordCall memberOrFunc
                    objExprOpt |> Option.iter (visitExpr localMutables)
                    argExprs |> List.iter (visitExpr localMutables)

            | Value _ -> ()

            | NewObject(objType, _, argExprs) ->
                recordCall objType
                argExprs |> List.iter (visitExpr localMutables)

            | Let((bindingVar, bindingExpr, _), bodyExpr) ->
                if bindingVar.IsMutable then
                    localMutables.Add(bindingKey bindingVar) |> ignore

                // let r = ref x  → treat r as a local cell (like let mutable)
                match bindingExpr with
                | Call(_, m, _, _, _) when isRefAlloc m -> localMutables.Add(bindingKey bindingVar) |> ignore
                | Application(Call(_, m, _, _, _), _, _) when isRefAlloc m ->
                    localMutables.Add(bindingKey bindingVar) |> ignore
                | _ -> ()

                visitExpr localMutables bindingExpr
                visitExpr localMutables bodyExpr

            | LetRec(recursiveBindings, bodyExpr) ->
                for (mfv, expr, _) in recursiveBindings do
                    if mfv.IsMutable then
                        localMutables.Add(bindingKey mfv) |> ignore

                    match expr with
                    | Call(_, m, _, _, _) when isRefAlloc m -> localMutables.Add(bindingKey mfv) |> ignore
                    | _ -> ()

                for (_, expr, _) in recursiveBindings do
                    visitExpr localMutables expr

                visitExpr localMutables bodyExpr

            | Lambda(_, bodyExpr) -> visitExpr localMutables bodyExpr

            | ValueSet(valToSet, valueExpr) ->
                // x <- e
                if not (localMutables.Contains(bindingKey valToSet)) then
                    markNonLocalMutation ()

                visitExpr localMutables valueExpr

            | AddressSet(lvalueExpr, rvalueExpr) ->
                markNonLocalMutation ()
                visitExpr localMutables lvalueExpr
                visitExpr localMutables rvalueExpr

            | FSharpFieldSet(objOpt, _, field, valueExpr) ->
                // ref.contents <- e  (sometimes how := is represented)
                let localRefWrite =
                    match objOpt with
                    | Some(Value v) when
                        localMutables.Contains(bindingKey v)
                        && field.Name.IndexOf("contents", StringComparison.OrdinalIgnoreCase) >= 0
                        ->
                        true
                    | _ -> false

                if not localRefWrite then
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
            | FSharpImplementationFileDeclaration.Entity(_, decls) -> decls |> List.iter visitDeclaration

            | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(v, _vs, body) ->
                let name = fullName v
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
            edges |> Seq.map (fun (KeyValue(k, v)) -> k, v |> Seq.toList) |> Map.ofSeq

        callGraph, (nonLocalMutation |> Set.ofSeq)

    /// isKnownPure: true when the name is in the composed pure index (foundational + library embeds).
    let isPure
        (isKnownPure: string -> bool)
        (callGraph: CallGraph)
        (nonLocalMutation: Set<string>)
        (name: string)
        =
        let rec check visited name =
            if Set.contains name visited then
                true
            elif Set.contains name nonLocalMutation then
                false
            elif isKnownPure name then
                true
            else
                match Map.tryFind name callGraph with
                | Some callees ->
                    let visited = Set.add name visited
                    callees |> List.forall (check visited)
                | None -> false

        check Set.empty name

    let findNonPure
        (isKnownPure: string -> bool)
        (callGraph: CallGraph)
        (nonLocalMutation: Set<string>)
        : Set<string> =
        callGraph
        |> Map.toSeq
        |> Seq.map fst
        |> Seq.filter (fun name -> not (isPure isKnownPure callGraph nonLocalMutation name))
        |> Set.ofSeq
