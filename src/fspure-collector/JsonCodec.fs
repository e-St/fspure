namespace FSharp.PureAnalyzer

open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open FSharp.PureSchema

/// DTO shapes used for JSON (de)serialisation of public artefacts.
module JsonCodec =

    // Re-export PureFile wire helpers from the shared schema library.
    let pureMethodToDto = PureFileIO.pureMethodToDto
    let pureFileToDto = PureFileIO.pureFileToDto
    let writePureFile (path: string) (file: PureFile) : unit = PureFileIO.write path file
    let readPureFile (path: string) : Result<PureFile, PureFileError> = PureFileIO.load path
    let parsePureFile (json: string) : Result<PureFile, PureFileError> = PureFileIO.parse json

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
            pureMethods: PureFileIO.PureMethodDto array
            /// Optional diagnostics dump (only when --verbose-report is set).
            diagnostics: MethodDiagDto array
        }

    let private options =
        let o = JsonSerializerOptions(WriteIndented = true)
        o.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
        o.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull
        o

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
        | null
        | "" -> ()
        | dir -> Directory.CreateDirectory(dir) |> ignore

        File.WriteAllText(path, json)
        ignore pureSet
