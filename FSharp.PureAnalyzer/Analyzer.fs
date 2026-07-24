namespace FSharp.PureAnalyzer

open System
open System.Collections.Generic
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

    /// Core analysis that works with any set of symbol uses + optional implementation files.
    let private analyze
        (fileName: string)
        (allSymbolUses: FSharpSymbolUse array)
        (implementationFiles: FSharpImplementationFileContents seq)
        (fileSymbolUses: FSharpSymbolUse seq)
        : Async<Message list> =
        async {
            let callGraph = buildCallGraph implementationFiles allSymbolUses
            let nonPure = findNonPure knownPure callGraph
            let messages = ResizeArray<Message>()

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

            return! analyze fileName allSymbolUses implementationFiles fileSymbolUses
        }

    /// File-level fallback used when FSAC does not supply CheckProjectResults.
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

            return! analyze fileName fileSymbolUses implementationFiles (Seq.ofArray fileSymbolUses)
        }

    [<EditorAnalyzer("FSharp.PureAnalyzer")>]
    let pureAnalyzerEditor (ctx: EditorContext) : Async<Message list> =
        async {
            match ctx.CheckProjectResults with
            | Some projectResults -> return! tryAnalyzeWithProjectResults ctx.FileName projectResults

            | None ->
                // FSAC frequently omits project results for performance.
                // Fall back to the file-level information that is almost always present.
                match ctx.CheckFileResults with
                | Some fileResults -> return! tryAnalyzeWithFileResults ctx.FileName fileResults ctx.TypedTree
                | None -> return []
        }

    [<CliAnalyzer("FSharp.PureAnalyzer")>]
    let pureAnalyzerCli (ctx: CliContext) : Async<Message list> =
        tryAnalyzeWithProjectResults ctx.FileName ctx.CheckProjectResults
