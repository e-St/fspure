namespace FSharp.PureAnalyzer

open System
open System.Collections.Generic
open System.Text
open FSharp.Analyzers.SDK
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open FSharp.PureAnalyzer.Analysis

module Analyzer =

    let private knownPure = PureSet.knownPure

    let private isCallableSymbol (symbol: FSharpSymbol) =
        match symbol with
        | :? FSharpMemberOrFunctionOrValue as v ->
            v.IsFunction
            || v.IsMember
            || v.IsConstructor
            || v.IsProperty
            || v.IsPropertyGetterMethod
            || v.IsPropertySetterMethod
        | _ -> false

    let private analyze
        (fileName: string)
        (allSymbolUses: FSharpSymbolUse array)
        (implementationFiles: FSharpImplementationFileContents seq)
        (fileSymbolUses: FSharpSymbolUse seq)
        (source: string) // "project" | "file" | "none"
        : Async<Message list> =
        async {
            let callGraph = buildCallGraph implementationFiles allSymbolUses
            let nonPure = findNonPure knownPure callGraph
            let messages = ResizeArray<Message>()

            // ----- DEBUG summary -------------------------------------------------------
            let mutable defCount = 0
            let mutable useCount = 0
            let defNames = ResizeArray<string>()
            let useNames = ResizeArray<string>()

            for su in allSymbolUses do
                match su.Symbol with
                | :? FSharpMemberOrFunctionOrValue as v when isCallableSymbol su.Symbol ->
                    let name = Name.fullNameOfMember v

                    if su.IsFromDefinition then
                        defCount <- defCount + 1

                        if defNames.Count < 16 then
                            defNames.Add(name)
                    else
                        useCount <- useCount + 1

                        if useNames.Count < 16 then
                            useNames.Add(name)
                | _ -> ()

            let implFileCount = implementationFiles |> Seq.length
            let graphSize = callGraph.Count
            let nonPureSize = nonPure.Count

            // Sample of edges for the first few graph nodes
            let edgeSamples =
                callGraph
                |> Map.toSeq
                |> Seq.truncate 8
                |> Seq.map (fun (caller, callees) ->
                    sprintf "%s -> [%s]" caller (String.Join("; ", callees |> List.truncate 6)))
                |> fun s -> String.Join(" || ", s)

            let sb = StringBuilder()
            sb.Append("DEBUG PureAnalyzer | source=").Append(source) |> ignore
            sb.Append(" | file=").Append(fileName) |> ignore
            sb.Append(" | implFiles=").Append(implFileCount) |> ignore
            sb.Append(" | symbolUses=").Append(allSymbolUses.Length) |> ignore
            sb.Append(" | defs=").Append(defCount) |> ignore
            sb.Append(" | uses=").Append(useCount) |> ignore
            sb.Append(" | graphNodes=").Append(graphSize) |> ignore
            sb.Append(" | nonPure=").Append(nonPureSize) |> ignore
            sb.Append(" | knownPure=").Append(knownPure.Count) |> ignore

            sb.Append(" | defNames=[").Append(String.Join("; ", defNames)).Append("]")
            |> ignore

            sb.Append(" | useNames=[").Append(String.Join("; ", useNames)).Append("]")
            |> ignore

            sb
                .Append(" | nonPureNames=[")
                .Append(String.Join("; ", nonPure |> Set.toArray |> Array.truncate 12))
                .Append("]")
            |> ignore

            sb.Append(" | edges=[").Append(edgeSamples).Append("]") |> ignore

            let summaryRange = Range.mkRange fileName (Position.mkPos 1 1) (Position.mkPos 1 2)

            messages.Add(
                {
                    Type = "Pure analyzer"
                    Message = sb.ToString()
                    Code = "PURE000"
                    Severity = Severity.Warning
                    Range = summaryRange
                    Fixes = []
                }
            )

            // DEBUG: mark every callable definition with its purity status
            for symbolUse in fileSymbolUses do
                if
                    symbolUse.IsFromDefinition
                    && symbolUse.FileName = fileName
                    && isCallableSymbol symbolUse.Symbol
                then
                    match symbolUse.Symbol with
                    | :? FSharpMemberOrFunctionOrValue as value ->
                        let name = Name.fullNameOfMember value
                        let inGraph = Map.containsKey name callGraph

                        let callees =
                            Map.tryFind name callGraph
                            |> Option.defaultValue []
                            |> fun xs -> String.Join("; ", xs |> List.truncate 8)

                        let inNonPure = Set.contains name nonPure

                        let msg =
                            sprintf
                                "[DEBUG def] %s | inGraph=%b | inNonPure=%b | callees=[%s]"
                                name
                                inGraph
                                inNonPure
                                callees

                        messages.Add(
                            {
                                Type = "Pure analyzer"
                                Message = msg
                                Code = "PURE000"
                                Severity = Severity.Warning
                                Range = symbolUse.Range
                                Fixes = []
                            }
                        )
                    | _ -> ()

            // ----- Real diagnostics ----------------------------------------------------
            // PURE001 – call sites of impure functions
            for symbolUse in fileSymbolUses do
                if
                    not symbolUse.IsFromDefinition
                    && symbolUse.FileName = fileName
                    && isCallableSymbol symbolUse.Symbol
                then
                    match symbolUse.Symbol with
                    | :? FSharpMemberOrFunctionOrValue as callee ->
                        let calleeName = Name.fullNameOfMember callee

                        if Set.contains calleeName nonPure then
                            messages.Add(Diagnostics.impureCall calleeName symbolUse.Range)
                    | _ -> ()

            // PURE002 – definitions that are transitively impure
            for symbolUse in fileSymbolUses do
                if
                    symbolUse.IsFromDefinition
                    && symbolUse.FileName = fileName
                    && isCallableSymbol symbolUse.Symbol
                then
                    match symbolUse.Symbol with
                    | :? FSharpMemberOrFunctionOrValue as value ->
                        let name = Name.fullNameOfMember value

                        if Set.contains name nonPure then
                            messages.Add(Diagnostics.impureFunction name symbolUse.Range)
                    | _ -> ()

            return messages |> Seq.toList
        }

    let private tryAnalyzeWithProjectResults (fileName: string) (projectResults: FSharpCheckProjectResults) =
        async {
            let allSymbolUses = projectResults.GetAllUsesOfAllSymbols() |> Seq.toArray
            let implementationFiles = projectResults.AssemblyContents.ImplementationFiles

            let fileSymbolUses =
                allSymbolUses |> Array.filter (fun su -> su.FileName = fileName) |> Seq.ofArray

            return! analyze fileName allSymbolUses implementationFiles fileSymbolUses "project"
        }

    let private tryAnalyzeWithFileResults
        (fileName: string)
        (fileResults: FSharpCheckFileResults)
        (typedTree: FSharpImplementationFileContents option)
        =
        async {
            let fileSymbolUses = fileResults.GetAllUsesOfAllSymbolsInFile() |> Seq.toArray

            let implementationFiles =
                match typedTree with
                | Some tree -> seq { tree }
                | None -> Seq.empty

            let source = if typedTree.IsSome then "file+tree" else "file-no-tree"

            return! analyze fileName fileSymbolUses implementationFiles (Seq.ofArray fileSymbolUses) source
        }

    [<EditorAnalyzer("FSharp.PureAnalyzer")>]
    let pureAnalyzerEditor (ctx: EditorContext) : Async<Message list> =
        async {
            match ctx.CheckProjectResults with
            | Some projectResults -> return! tryAnalyzeWithProjectResults ctx.FileName projectResults

            | None ->
                match ctx.CheckFileResults with
                | Some fileResults -> return! tryAnalyzeWithFileResults ctx.FileName fileResults ctx.TypedTree
                | None ->
                    let r = Range.mkRange ctx.FileName (Position.mkPos 1 1) (Position.mkPos 1 2)

                    return
                        [
                            {
                                Type = "Pure analyzer"
                                Message =
                                    "DEBUG PureAnalyzer | CheckProjectResults=None AND CheckFileResults=None | TypedTree="
                                    + string ctx.TypedTree.IsSome
                                Code = "PURE000"
                                Severity = Severity.Warning
                                Range = r
                                Fixes = []
                            }
                        ]
        }

    [<CliAnalyzer("FSharp.PureAnalyzer")>]
    let pureAnalyzerCli (ctx: CliContext) : Async<Message list> =
        tryAnalyzeWithProjectResults ctx.FileName ctx.CheckProjectResults
