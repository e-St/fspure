/// CommonJS surface for extension.js (require("./logic")).
module Exports

open Fable.Core
open Fable.Core.JsInterop
open Fspure.DecorationLogic

type private DiagBag =
    {
        Code: string
        Source: string
        Line: int option
    }

let private readLine (range: obj) : int option =
    try
        if isNull range then
            None
        else
            let start = range?start

            if isNull start then None
            else Some(int (start?line : float))
    with _ ->
        None

let private readCode (code: obj) : string =
    Logic.diagnosticCode code

let private toLike (d: obj) : Logic.DiagnosticLike =
    let code = readCode (d?code)
    let source: string =
        let s = d?source
        if isNull s then "" else string s

    {
        Code = code
        Source = source
        Line = readLine (d?range)
    }

let diagnosticCodeJs (code: obj) = Logic.diagnosticCode code

let isPureAnalyzerDiagnosticJs (d: obj) =
    Logic.isPureAnalyzerDiagnostic (toLike d)

let badgesByLineJs (diagnostics: obj[]) =
    let likes = diagnostics |> Array.map toLike
    let map = Logic.badgesByLine likes
    let m = JS.Constructors.Map.Create()

    for KeyValue(line, entry) in map do
        m.set (
            line,
            createObj
                [
                    "badge" ==> entry.Badge
                    "code" ==> entry.Code
                ]
        )
        |> ignore

    m

let badgeForDefinitionCodeJs (code: string) =
    match Logic.badgeForDefinitionCode code with
    | Some b -> box b
    | None -> null

// Attach exports for require("./logic")
emitJsStatement
    ()
    """
module.exports = {
  diagnosticCode: diagnosticCodeJs,
  isPureAnalyzerDiagnostic: isPureAnalyzerDiagnosticJs,
  badgesByLine: badgesByLineJs,
  badgeForDefinitionCode: badgeForDefinitionCodeJs,
  BADGE_IMPURE: "impure",
  BADGE_PURE: "pure",
  IMPURE_CODES: new Set(["PURE001", "PURE002"]),
  PURE_CODES: new Set(["PURE003"])
};
"""
