namespace FSharp.PureAnalyzer

open System
open FSharp.Compiler.Symbols

/// Normalise F# symbols to the same `{Namespace}.{Type}.{Member}` shape used by
/// the purity-collector whitelist.
module Name =

    let fullNameOfMember (value: FSharpMemberOrFunctionOrValue) : string =
        let typeName =
            value.DeclaringEntity
            |> Option.map (fun entity -> entity.FullName)
            |> Option.defaultValue ""

        let memberName = value.LogicalName

        if String.IsNullOrEmpty typeName then
            memberName
        else
            $"%s{typeName}.%s{memberName}"
