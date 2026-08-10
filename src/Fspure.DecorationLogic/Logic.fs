namespace Fspure.DecorationLogic

open System
open System.Collections.Generic

#if FABLE_COMPILER
open Fable.Core.JsInterop
#endif

/// Pure decoration rules shared by the VS Code extension (via Fable) and .NET tests.
module Logic =

    let impureCodes = set [ "PURE001"; "PURE002" ]
    let pureCodes = set [ "PURE003" ]

    let badgeImpure = "impure"
    let badgePure = "pure"

    /// Normalize diagnostic code from string or { value: string } shape.
    let diagnosticCode (code: obj) : string =
#if FABLE_COMPILER
        if isNull code then
            ""
        elif jsTypeof code = "string" then
            unbox<string> code
        else
            let v: obj = code?value

            if isNull v then "" else string v
#else
        match code with
        | null -> ""
        | :? string as s -> s
        | o ->
            let t = o.GetType()
            let p = t.GetProperty("value")

            if isNull p then string o
            else
                match p.GetValue(o) with
                | null -> ""
                | v -> string v
#endif

    type DiagnosticLike =
        {
            Code: string
            Source: string
            Line: int option
        }

    let isPureAnalyzerDiagnostic (d: DiagnosticLike) : bool =
        let code = d.Code
        let source = d.Source

        impureCodes.Contains code
        || pureCodes.Contains code
        || source.IndexOf("Pure analyzer", StringComparison.Ordinal) >= 0
        || source.IndexOf("FSharp.PureAnalyzer", StringComparison.Ordinal) >= 0

    type BadgeEntry = { Badge: string; Code: string }

    /// Map definition diagnostics to per-line badges.
    /// Only PURE002 / PURE003 produce badges (PURE001 is call-site only).
    /// Impure wins over pure on the same line.
    let badgesByLine (diagnostics: DiagnosticLike seq) : Map<int, BadgeEntry> =
        let byLine = Dictionary<int, string option * string option>()

        for d in diagnostics do
            if isPureAnalyzerDiagnostic d then
                match d.Line with
                | None -> ()
                | Some line ->
                    let code = d.Code

                    let impure', pure' =
                        match byLine.TryGetValue line with
                        | true, v -> v
                        | _ -> None, None

                    let next =
                        if code = "PURE002" then Some code, pure'
                        elif code = "PURE003" then impure', Some code
                        else impure', pure'

                    byLine[line] <- next

        [
            for KeyValue(line, (impure', pure')) in byLine do
                match impure' with
                | Some c -> yield line, { Badge = badgeImpure; Code = c }
                | None ->
                    match pure' with
                    | Some c -> yield line, { Badge = badgePure; Code = c }
                    | None -> ()
        ]
        |> Map.ofList

    let badgeForDefinitionCode (code: string) : string option =
        if code = "PURE002" then Some badgeImpure
        elif code = "PURE003" then Some badgePure
        else None
