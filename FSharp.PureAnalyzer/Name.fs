namespace FSharp.PureAnalyzer

open System
open FSharp.Compiler.Symbols

/// Normalise F# symbols to the same `{Namespace}.{Type}.{Member}` shape used by
/// the purity-collector whitelist.
module Name =

    /// Prefer CompiledName (usually PascalCase, matches IL) over LogicalName
    /// (often camelCase for let-bound module functions).
    let private memberNameOf (value: FSharpMemberOrFunctionOrValue) =
        if not (String.IsNullOrEmpty value.CompiledName) then
            value.CompiledName
        else
            value.LogicalName

    let fullNameOfMember (value: FSharpMemberOrFunctionOrValue) : string =
        let typeName =
            value.DeclaringEntity
            |> Option.map (fun entity -> entity.FullName)
            |> Option.defaultValue ""

        let memberName = memberNameOf value

        if String.IsNullOrEmpty typeName then
            memberName
        else
            $"%s{typeName}.%s{memberName}"
