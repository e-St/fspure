namespace FSharp.PureAnalyzer

open System
open System.Collections.Generic
open FSharp.Analyzers.SDK
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open FSharp.PureAnalyzer.Analysis

/// Entry points for the FSharp.Analyzers.SDK.
module Analyzer =

    let private knownPure = PureSet.knownPure

    let private implementationFiles (projectResults: FSharpCheckProjectResults) =
        projectResults.AssemblyContents.ImplementationFiles

    let private analyzeFile
        (fileName: string)
        (projectResults: FSharpCheckProjectResults)
        (fileSymbolUses: FSharpSymbolUse seq)
        : Async<Message list> =
        async {
            let callGraph = buildCallGraph (implementationFiles projectResults)
            let nonPure = findNonPure knownPure callGraph
            let messages = ResizeArray<Message>()

            let isCallableSymbol (symbol: FSharpSymbol) =
                match symbol with
                | :? FSharpMemberOrFunctionOrValue as v -> v.IsFunction || v.IsMember || v.IsConstructor
                | _ -> false

            // PURE001: flag non-pure call sites in the current file.
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

            // PURE002: flag non-pure function definitions in the current file.
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
            let symbolUses = projectResults.GetAllUsesOfAllSymbols()
            return! analyzeFile fileName projectResults symbolUses
        }

    [<EditorAnalyzer("FSharp.PureAnalyzer")>]
    let pureAnalyzerEditor (ctx: EditorContext) : Async<Message list> =
        async {
            match ctx.CheckProjectResults with
            | None -> return []
            | Some projectResults -> return! tryAnalyzeWithProjectResults ctx.FileName projectResults
        }

    [<CliAnalyzer("FSharp.PureAnalyzer")>]
    let pureAnalyzerCli (ctx: CliContext) : Async<Message list> =
        tryAnalyzeWithProjectResults ctx.FileName ctx.CheckProjectResults
