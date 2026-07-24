namespace FSharp.PureAnalyzer

open FSharp.Analyzers.SDK
open FSharp.Compiler.Text

/// Diagnostic factory for the pure-function analyzer.
module Diagnostics =

    let private mkMessage code severity message range =
        {
            Type = "Pure analyzer"
            Message = message
            Code = code
            Severity = severity
            Range = range
            Fixes = []
        }

    /// PURE001: a call site invokes a function that is not known to be pure.
    /// Hint severity → minimal editor underline (badge is the primary UI).
    let impureCall (calleeName: string) (range: range) : Message =
        mkMessage "PURE001" Severity.Hint $"Call to '%s{calleeName}' is not known to be pure." range

    /// PURE002: a declared function is not transitively pure.
    let impureFunction (functionName: string) (range: range) : Message =
        mkMessage "PURE002" Severity.Hint $"Function '%s{functionName}' is not transitively pure." range

    /// PURE003: a declared function is transitively pure.
    let pureFunction (functionName: string) (range: range) : Message =
        mkMessage "PURE003" Severity.Hint $"Function '%s{functionName}' is transitively pure." range
