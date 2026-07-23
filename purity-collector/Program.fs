open System
open System.CommandLine
open System.CommandLine.Invocation
open System.IO
open FSharp.PureAnalyzer

module Cli =
    let generatorBanner () =
        printfn $"{Constants.Generator}"
        printfn "List A collector – static purity analysis of FSharp.Core / BCL"
        printfn ""

    let resolveAssemblies (explicitPaths: string list) (fromDefaults: bool) : string list =
        let defaults = if fromDefaults then IlAnalyzer.defaultAssemblyPaths () else []

        (explicitPaths @ defaults)
        |> List.filter (fun p -> not (String.IsNullOrWhiteSpace p) && File.Exists p)
        |> List.distinctBy (fun p -> Path.GetFullPath p)

    let runListA
        (outputPath: string)
        (reportPath: string option)
        (assemblyPaths: string list)
        (packageId: string)
        (packageVersion: string)
        (publicOnly: bool)
        (verboseReport: bool)
        : int =
        generatorBanner ()

        if assemblyPaths.IsEmpty then
            eprintfn "error: no assemblies to analyze. Pass --assembly or use --defaults."
            2
        else
            printfn "Assemblies:"

            for p in assemblyPaths do
                printfn $"  - {p}"

            printfn ""
            printfn "Analyzing IL and building call graphs..."

            let methods, analyzed = IlAnalyzer.analyzeAssemblies assemblyPaths

            printfn $"Discovered {methods.Length} methods in {analyzed.Length} assembly/assemblies."
            printfn "Computing pure fixed-point (List A)..."

            let pureFile, pureSet, _byName =
                PurityEngine.buildListA methods packageId packageVersion publicOnly

            printfn $"List A pure methods: {pureFile.PureMethods.Length} (set size before public filter: {pureSet.Count})"

            let outFull = Path.GetFullPath outputPath
            JsonCodec.writePureFile outFull pureFile
            printfn $"Wrote pure whitelist: {outFull}"

            match reportPath with
            | Some rp ->
                let rpFull = Path.GetFullPath rp

                JsonCodec.writeListAReport
                    rpFull
                    packageId
                    packageVersion
                    analyzed
                    methods
                    pureFile.PureMethods
                    verboseReport

                printfn $"Wrote List A report: {rpFull}"
            | None -> ()

            printfn "Done."
            0

open Cli

[<EntryPoint>]
let main argv =
    let outputOption =
        Option<string>(
            aliases = [| "-o"; "--output" |],
            description = "Path to the List A .pure.json output file",
            getDefaultValue = fun () -> "list-a.pure.json"
        )

    let reportOption =
        Option<string>(
            aliases = [| "--report" |],
            description = "Optional path for a detailed List A JSON report"
        )

    let assemblyOption =
        Option<string[]>(
            aliases = [| "-a"; "--assembly" |],
            description = "Assembly path(s) to analyze (repeatable)"
        )

    assemblyOption.AllowMultipleArgumentsPerToken <- true

    let defaultsOption =
        Option<bool>(
            aliases = [| "--defaults" |],
            description = "Include the default foundational assembly set (FSharp.Core + core BCL)",
            getDefaultValue = fun () -> true
        )

    let packageIdOption =
        Option<string>(
            aliases = [| "--package-id" |],
            description = "packageId written into the .pure.json",
            getDefaultValue = fun () -> "FSharp.Core+BCL"
        )

    let packageVersionOption =
        Option<string>(
            aliases = [| "--package-version" |],
            description = "packageVersion written into the .pure.json",
            getDefaultValue = fun () ->
                match typeof<unit>.Assembly.GetName().Version with
                | null -> "0.0.0"
                | v -> $"{v.Major}.{v.Minor}.{v.Build}"
        )

    let publicOnlyOption =
        Option<bool>(
            aliases = [| "--public-only" |],
            description = "Only emit publicly visible pure methods",
            getDefaultValue = fun () -> true
        )

    let verboseReportOption =
        Option<bool>(
            aliases = [| "--verbose-report" |],
            description = "Include per-method diagnostics in the report",
            getDefaultValue = fun () -> false
        )

    let root =
        RootCommand("purity-collector – generate List A (foundational pure set) from FSharp.Core / BCL")

    root.AddOption outputOption
    root.AddOption reportOption
    root.AddOption assemblyOption
    root.AddOption defaultsOption
    root.AddOption packageIdOption
    root.AddOption packageVersionOption
    root.AddOption publicOnlyOption
    root.AddOption verboseReportOption

    root.SetHandler(fun (ctx: InvocationContext) ->
        let pr = ctx.ParseResult

        let output = pr.GetValueForOption<string>(outputOption)
        let report = pr.GetValueForOption<string>(reportOption)
        let assemblies = pr.GetValueForOption<string[]>(assemblyOption)

        // Boolean options: read via GetResult to avoid FS3265 nullable-bool warnings.
        let boolOrDefault (opt: Option<bool>) (fallback: bool) =
            match pr.FindResultFor opt with
            | null -> fallback
            | r -> r.GetValueOrDefault<bool>()

        let useDefaults = boolOrDefault defaultsOption true
        let packageId = pr.GetValueForOption<string>(packageIdOption)
        let packageVersion = pr.GetValueForOption<string>(packageVersionOption)
        let publicOnly = boolOrDefault publicOnlyOption true
        let verboseReport = boolOrDefault verboseReportOption false

        let asmList =
            let explicitPaths =
                match assemblies with
                | null -> []
                | arr -> arr |> Array.toList

            resolveAssemblies explicitPaths useDefaults

        let reportOpt =
            match report with
            | null | "" -> None
            | rp when String.IsNullOrWhiteSpace rp -> None
            | rp -> Some rp

        let outputPath =
            match output with
            | null | "" -> "list-a.pure.json"
            | o -> o

        let pkgId =
            match packageId with
            | null | "" -> "FSharp.Core+BCL"
            | p -> p

        let pkgVer =
            match packageVersion with
            | null | "" -> "0.0.0"
            | p -> p

        let code =
            runListA outputPath reportOpt asmList pkgId pkgVer publicOnly verboseReport

        ctx.ExitCode <- code)

    root.Invoke argv
