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

    let private implementationFiles (projectResults: FSharpCheckProjectResults) =
        projectResults.AssemblyContents.ImplementationFiles

    let private analyzeFile
        (fileName: string)
        (projectResults: FSharpCheckProjectResults)
        (fileSymbolUses: FSharpSymbolUse seq)
        : Async<Message list> =
        async {
            let allSymbolUses = projectResults.GetAllUsesOfAllSymbols() |> Seq.toArray
            let callGraph = buildCallGraph (implementationFiles projectResults) allSymbolUses
            let nonPure = findNonPure knownPure callGraph
            let messages = ResizeArray<Message>()

            let isCallableSymbol (symbol: FSharpSymbol) =
                match symbol with
                | :? FSharpMemberOrFunctionOrValue as v ->
                    v.IsFunction
                    || v.IsMember
                    || v.IsConstructor
                    || v.IsProperty
                    || v.IsPropertyGetterMethod
                    || v.IsPropertySetterMethod
                | _ -> false

            // ----- Collect debug stats -------------------------------------------------
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
                        if defNames.Count < 12 then defNames.Add(name)
                    else
                        useCount <- useCount + 1
                        if useNames.Count < 12 then useNames.Add(name)
                | _ -> ()

            let implFileCount = implementationFiles projectResults |> Seq.length
            let graphSize = callGraph.Count
            let nonPureSize = nonPure.Count
            let pureSetSize = knownPure.Count

            let sb = StringBuilder()
            sb.AppendFormat(\"DEBUG PureAnalyzer | file={0}\", fileName) |> ignore
            sb.AppendFormat(\" | implFiles={0}\", implFileCount) |> ignore
            sb.AppendFormat(\" | symbolUses={0}\", allSymbolUses.Length) |> ignore
            sb.AppendFormat(\" | defs={0}\", defCount) |> ignore
            sb.AppendFormat(\" | uses={0}\", useCount) |> ignore
            sb.AppendFormat(\" | graphNodes={0}\", graphSize) |> ignore
            sb.AppendFormat(\" | nonPure={0}\", nonPureSize) |> ignore
            sb.AppendFormat(\" | knownPure={0}\", pureSetSize) |> ignore
            sb.Append(\" | defNames=[\").Append(String.Join(\"; \", defNames)).Append(\"]\") |> ignore
            sb.Append(\" | useNames=[\").Append(String.Join(\"; \", useNames)).Append(\"]\") |> ignore
            sb.Append(\" | nonPureNames=[\").Append(String.Join(\"; \", nonPure |> Set.toArray |> Array.truncate 12)).Append(\"]\") |> ignore

            // Emit a single summary diagnostic at the top of the file (line 1)
            let summaryRange =
                Range.mkRange fileName (Position.mkPos 1 1) (Position.mkPos 1 2)
            messages.Add(
                { Type = \"Pure analyzer\"
                  Message = sb.ToString()
                  Code = \"PURE000\"
                  Severity = Severity.Warning
                  Range = summaryRange
                  Fixes = [] })

            // Also flag every callable definition so we can see which ones are visible
            for symbolUse in fileSymbolUses do
                if
                    symbolUse.IsFromDefinition
                    && symbolUse.FileName = fileName
                    && isCallableSymbol symbolUse.Symbol
                then
                    match symbolUse.Symbol with
                    | :? FSharpMemberOrFunctionOrValue as value ->
                        let name = Name.fullNameOfMember value
                        let inNonPure = Set.contains name nonPure
                        let msg =
                            sprintf \"Function \"%s\" [DEBUG def] inNonPure=%b\" name inNonPure
                        messages.Add(Diagnostics.impureFunction msg symbolUse.Range)
                    | _ -> ()

            // Original purity-based diagnostics
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

    [<EditorAnalyzer(\"FSharp.PureAnalyzer\")>]
    let pureAnalyzerEditor (ctx: EditorContext) : Async<Message list> =
        async {
            match ctx.CheckProjectResults with
            | None ->
                // Even when there are no project results, emit a diagnostic so we know
                let r = Range.mkRange ctx.FileName (Position.mkPos 1 1) (Position.mkPos 1 2)
                return
                    [ { Type = \"Pure analyzer\"
                        Message = \"DEBUG PureAnalyzer | CheckProjectResults = None\"
                        Code = \"PURE000\"
                        Severity = Severity.Warning
                        Range = r
                        Fixes = [] } ]
            | Some projectResults ->
                return! tryAnalyzeWithProjectResults ctx.FileName projectResults
        }

    [<CliAnalyzer(\"FSharp.PureAnalyzer\")>]
    let pureAnalyzerCli (ctx: CliContext) : Async<Message list> =
        tryAnalyzeWithProjectResults ctx.FileName ctx.CheckProjectResults
