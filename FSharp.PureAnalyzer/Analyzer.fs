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


    // Set to true while developing; set to false for normal use.
    let private emitDebugSummary = false

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
        (source: string)
        (pureIndex: PureSet.Index)
        : Async<Message list> =
        async {
            let isKnownPure name = PureSet.containsIn pureIndex name
            let callGraph, nonLocalMutation = buildCallGraph implementationFiles allSymbolUses
            let nonPure = findNonPure isKnownPure callGraph nonLocalMutation
            let messages = ResizeArray<Message>()

            if emitDebugSummary then
                let graphSize = callGraph.Count
                let nonPureSize = nonPure.Count

                let edgeSamples =
                    callGraph
                    |> Map.toSeq
                    |> Seq.truncate 10
                    |> Seq.map (fun (caller, callees) ->
                        sprintf "%s -> [%s]" caller (String.Join("; ", callees |> List.truncate 6)))
                    |> fun s -> String.Join(" || ", s)

                let sb = StringBuilder()
                sb.Append("DEBUG PureAnalyzer | source=").Append(source) |> ignore
                sb.Append(" | implFiles=").Append(implementationFiles |> Seq.length) |> ignore
                sb.Append(" | graphNodes=").Append(graphSize) |> ignore
                sb.Append(" | nonPure=").Append(nonPureSize) |> ignore
                sb.Append(" | mutations=").Append(nonLocalMutation.Count) |> ignore

                sb
                    .Append(" | nonPureNames=[")
                    .Append(String.Join("; ", nonPure |> Set.toArray |> Array.truncate 16))
                    .Append("]")
                |> ignore

                sb.Append(" | edges=[").Append(edgeSamples).Append("]") |> ignore

                let summaryRange = Range.mkRange fileName (Position.mkPos 1 1) (Position.mkPos 1 2)

                messages.Add(
                    {
                        Type = "Pure analyzer"
                        Message = sb.ToString()
                        Code = "PURE000"
                        Severity = Severity.Hint
                        Range = summaryRange
                        Fixes = []
                    }
                )

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

            // PURE002 / PURE003 – definitions (at most one diagnostic per name)
            let seenDefs = HashSet<string>(StringComparer.Ordinal)

            for symbolUse in fileSymbolUses do
                if
                    symbolUse.IsFromDefinition
                    && symbolUse.FileName = fileName
                    && isCallableSymbol symbolUse.Symbol
                then
                    match symbolUse.Symbol with
                    | :? FSharpMemberOrFunctionOrValue as value ->
                        let name = Name.fullNameOfMember value

                        // Only report definitions that appear in the call graph
                        // (skips compiler-generated clo* / arg* helpers).
                        if Map.containsKey name callGraph && seenDefs.Add(name) then
                            if Set.contains name nonPure then
                                messages.Add(Diagnostics.impureFunction name symbolUse.Range)
                            else
                                messages.Add(Diagnostics.pureFunction name symbolUse.Range)
                    | _ -> ()

            return messages |> Seq.toList
        }

    let private loadPureIndex
        (projectOptions: AnalyzerProjectOptions)
        (projectResults: FSharpCheckProjectResults option)
        : PureSet.Index =
        try
            // Precedence: overrides > library embeds > foundational
            // (see PureManifestLoader; fspure.overrides.json + FSPURE_DISABLE_FOUNDATIONAL).
            (PureManifestLoader.loadForAnalysis (Some projectOptions) projectResults).Index
        with _ ->
            // Never fail analysis because of manifest discovery.
            PureSet.foundationalIndex ()

    let private tryAnalyzeWithProjectResults
        (fileName: string)
        (projectOptions: AnalyzerProjectOptions)
        (projectResults: FSharpCheckProjectResults)
        =
        async {
            let pureIndex = loadPureIndex projectOptions (Some projectResults)
            let allSymbolUses = projectResults.GetAllUsesOfAllSymbols() |> Seq.toArray
            let implementationFiles = projectResults.AssemblyContents.ImplementationFiles

            let fileSymbolUses =
                allSymbolUses |> Array.filter (fun su -> su.FileName = fileName) |> Seq.ofArray

            return! analyze fileName allSymbolUses implementationFiles fileSymbolUses "project" pureIndex
        }

    let private tryAnalyzeWithFileResults
        (fileName: string)
        (projectOptions: AnalyzerProjectOptions)
        (fileResults: FSharpCheckFileResults)
        (typedTree: FSharpImplementationFileContents option)
        (projectResults: FSharpCheckProjectResults option)
        =
        async {
            let pureIndex = loadPureIndex projectOptions projectResults
            let fileSymbolUses = fileResults.GetAllUsesOfAllSymbolsInFile() |> Seq.toArray

            let implementationFiles =
                match typedTree with
                | Some tree -> seq { tree }
                | None -> Seq.empty

            let source = if typedTree.IsSome then "file+tree" else "file-no-tree"

            return! analyze fileName fileSymbolUses implementationFiles (Seq.ofArray fileSymbolUses) source pureIndex
        }

    [<EditorAnalyzer("FSharp.PureAnalyzer")>]
    let pureAnalyzerEditor (ctx: EditorContext) : Async<Message list> =
        async {
            match ctx.CheckProjectResults with
            | Some projectResults ->
                return! tryAnalyzeWithProjectResults ctx.FileName ctx.ProjectOptions projectResults

            | None ->
                match ctx.CheckFileResults with
                | Some fileResults ->
                    return!
                        tryAnalyzeWithFileResults
                            ctx.FileName
                            ctx.ProjectOptions
                            fileResults
                            ctx.TypedTree
                            None
                | None -> return []
        }

    [<CliAnalyzer("FSharp.PureAnalyzer")>]
    let pureAnalyzerCli (ctx: CliContext) : Async<Message list> =
        tryAnalyzeWithProjectResults ctx.FileName ctx.ProjectOptions ctx.CheckProjectResults
