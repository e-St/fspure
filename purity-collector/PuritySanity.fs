namespace FSharp.PureAnalyzer

open System

/// Post-conditions on the computed pure set to catch false pures.
module PuritySanity =

    let private norm = ImpurityRules.normalizeName

    let private sameName a b =
        a = b || norm a = norm b

    /// Methods that must never appear as pure (core impurity oracle).
    /// Use IL-style / CompiledName forms where possible.
    let private mustBeImpure =
        set
            [
                "System.Console.WriteLine"
                "System.Console.Write"
                "System.Console.ReadLine"
                "System.Environment.GetEnvironmentVariable"
                "System.Environment.get_MachineName"
                "System.IO.File.ReadAllText"
                "System.IO.File.WriteAllText"
                "System.IO.Directory.CreateDirectory"
                "System.DateTime.get_Now"
                "System.DateTime.get_UtcNow"
                "System.Random.Next"
                "Microsoft.FSharp.Core.ExtraTopLevelOperators.PrintFormatLine"
                "Microsoft.FSharp.Core.ExtraTopLevelOperators.PrintFormat"
                "Microsoft.FSharp.Core.ExtraTopLevelOperators.PrintFormatToTextWriter"
                "Microsoft.FSharp.Core.ExtraTopLevelOperators.PrintFormatLineToTextWriter"
            ]

    /// Must appear pure after HOF fix (regression guards).
    let private mustBePure =
        set
            [
                "Microsoft.FSharp.Collections.ListModule.Map"
                "Microsoft.FSharp.Collections.ListModule.Filter"
                "Microsoft.FSharp.Collections.ListModule.IsEmpty"
                "Microsoft.FSharp.Collections.ListModule.Append"
                "Microsoft.FSharp.Collections.ListModule.Fold"
                "Microsoft.FSharp.Core.OptionModule.Map"
                "Microsoft.FSharp.Core.Operators.op_Addition"
            ]

    type SanityReport =
        {
            FalsePures: string list
            MissingPures: string list
            Ok: bool
        }

    let private setContainsName (pureSet: Set<string>) (name: string) =
        pureSet |> Set.exists (fun p -> sameName p name)

    let check (pureSet: Set<string>) : SanityReport =
        // False pure: name (or normalize-equal) is in the pure set.
        // Also fail if isKnownPureLeaf incorrectly treats it as a leaf.
        let falsePures =
            mustBeImpure
            |> Set.toList
            |> List.filter (fun n ->
                setContainsName pureSet n
                || ImpurityRules.isKnownPureLeaf n)
            |> List.sort

        // Missing pure: not in pure set AND not in CollectionLeaves seed
        // (CollectionLeaves guarantees export even when IL body is "locally impure").
        let missingPures =
            mustBePure
            |> Set.toList
            |> List.filter (fun n ->
                not (setContainsName pureSet n)
                && not (CollectionLeaves.all |> Set.exists (fun c -> sameName c n)))
            |> List.sort

        {
            FalsePures = falsePures
            MissingPures = missingPures
            Ok = falsePures.IsEmpty
        }

    let print (report: SanityReport) =
        if not report.FalsePures.IsEmpty then
            eprintfn "SANITY FAIL: false pures detected:"
            for n in report.FalsePures do
                eprintfn "  - %s" n

        if not report.MissingPures.IsEmpty then
            eprintfn "SANITY WARN: expected pure methods missing from set:"
            for n in report.MissingPures do
                eprintfn "  - %s" n

        if report.Ok && report.MissingPures.IsEmpty then
            printfn "SANITY OK: no false pures; required pure methods present."
        elif report.Ok then
            printfn "SANITY OK (false-pure check): no false pures."
