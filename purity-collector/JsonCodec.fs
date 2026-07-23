namespace FSharp.PureAnalyzer

open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization

/// DTO shapes used for JSON (de)serialisation of public artefacts.
module JsonCodec =

    [<CLIMutable>]
    type PureMethodDto =
        {
            fullName: string
            origin: string
            comment: string | null
        }

    [<CLIMutable>]
    type PureFileDto =
        {
            schemaVersion: string
            packageId: string
            packageVersion: string
            generatedAt: string
            generator: string
            pureMethods: PureMethodDto array
        }

    [<CLIMutable>]
    type MethodDiagDto =
        {
            fullName: string
            assemblyName: string
            isPublic: bool
            isStatic: bool
            hasBody: bool
            hasLocalImpurity: bool
            impurityReasons: string array
            callees: string array
        }

    [<CLIMutable>]
    type ListAReportDto =
        {
            schemaVersion: string
            generatedAt: string
            generator: string
            packageId: string
            packageVersion: string
            analyzedAssemblies: string array
            totalMethods: int
            pureMethodCount: int
            impureMethodCount: int
            pureMethods: PureMethodDto array
            /// Optional diagnostics dump (only when --verbose-report is set).
            diagnostics: MethodDiagDto array
        }

    let private options =
        let o = JsonSerializerOptions(WriteIndented = true)
        o.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
        o.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull
        o

    let private originToDto (origin: PureOrigin) : string * string option =
        match origin with
        | Automatic -> "automatic", None
        | Manual None -> "manual", None
        | Manual(Some c) -> "manual", Some c

    let private pureMethodToDto (m: PureMethod) : PureMethodDto =
        let origin, comment = originToDto m.Origin

        {
            fullName = m.FullName
            origin = origin
            comment =
                match comment with
                | None -> null
                | Some c -> c
        }

    let pureFileToDto (file: PureFile) : PureFileDto =
        {
            schemaVersion = file.SchemaVersion
            packageId = file.PackageId
            packageVersion = file.PackageVersion
            generatedAt = file.GeneratedAt.ToString("o")
            generator = file.Generator
            pureMethods = file.PureMethods |> List.map pureMethodToDto |> Array.ofList
        }

    let writePureFile (path: string) (file: PureFile) : unit =
        let dto = pureFileToDto file
        let json = JsonSerializer.Serialize(dto, options)
        match Path.GetDirectoryName path with
        | null | "" -> ()
        | dir -> Directory.CreateDirectory(dir) |> ignore

        File.WriteAllText(path, json)

    let writeListAReport
        (path: string)
        (packageId: string)
        (packageVersion: string)
        (assemblies: string list)
        (allMethods: AnalyzedMethod list)
        (pureMethods: PureMethod list)
        (includeDiagnostics: bool)
        : unit =
        let pureSet = pureMethods |> List.map _.FullName |> Set.ofList

        let diags =
            if includeDiagnostics then
                allMethods
                |> List.map (fun m ->
                    {
                        fullName = m.FullName
                        assemblyName = m.AssemblyName
                        isPublic = m.IsPublic
                        isStatic = m.IsStatic
                        hasBody = m.HasBody
                        hasLocalImpurity = m.HasLocalImpurity
                        impurityReasons = Array.ofList m.ImpurityReasons
                        callees = Array.ofList m.Callees
                    })
                |> Array.ofList
            else
                [||]

        let dto =
            {
                schemaVersion = Constants.SchemaVersion
                generatedAt = DateTimeOffset.UtcNow.ToString("o")
                generator = Constants.Generator
                packageId = packageId
                packageVersion = packageVersion
                analyzedAssemblies = Array.ofList assemblies
                totalMethods = allMethods.Length
                pureMethodCount = pureMethods.Length
                impureMethodCount = allMethods.Length - pureMethods.Length
                pureMethods = pureMethods |> List.map pureMethodToDto |> Array.ofList
                diagnostics = diags
            }

        let json = JsonSerializer.Serialize(dto, options)

        match Path.GetDirectoryName path with
        | null | "" -> ()
        | dir -> Directory.CreateDirectory(dir) |> ignore

        File.WriteAllText(path, json)
        ignore pureSet
