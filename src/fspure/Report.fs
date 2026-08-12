namespace Fspure.Cli

open System
open System.IO
open System.Text
open System.Text.Json

/// Deterministic JSON / SARIF writers. Same inputs → byte-identical output.
module Report =

    let utf8NoBom = UTF8Encoding(false, true)

    let private writeString (w: Utf8JsonWriter) (name: string) (value: string) =
        w.WriteString(name, value)

    let impureCallsOf (items: Diagnostic list) =
        items
        |> List.filter (fun d -> d.Code = Constants.CallCode)
        |> Diagnostic.sort

    let summaryOf (items: Diagnostic list) =
        let calls = impureCallsOf items
        let callers =
            calls
            |> List.map _.Caller
            |> List.filter (fun s -> s <> "")
            |> List.distinct
            |> List.length

        calls.Length, callers

    let writeJson
        (project: string)
        (focus: string list)
        (ignore: string list)
        (failOnImpure: bool)
        (items: Diagnostic list)
        : byte[] =
        let items = Diagnostic.sort items
        let calls = impureCallsOf items
        let callN, callerN = summaryOf items
        let focus = focus |> List.map Paths.normalize |> List.filter ((<>) "") |> List.sort
        let ignore = ignore |> List.map Paths.normalize |> List.filter ((<>) "") |> List.sort
        let project = Paths.normalize project

        use stream = new MemoryStream()

        use writer =
            new Utf8JsonWriter(
                stream,
                JsonWriterOptions(Indented = false, SkipValidation = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping)
            )

        writer.WriteStartObject()
        writeString writer "$schema" Constants.SchemaId
        writeString writer "schemaVersion" Constants.SchemaVersion
        writeString writer "tool" Constants.ToolName
        writeString writer "toolVersion" Constants.ToolVersion
        writeString writer "project" project

        writer.WriteStartArray "focus"

        for f in focus do
            writer.WriteStringValue f

        writer.WriteEndArray()

        writer.WriteStartArray "ignore"

        for i in ignore do
            writer.WriteStringValue i

        writer.WriteEndArray()

        writer.WriteStartObject "summary"
        writer.WriteNumber("impureCalls", callN)
        writer.WriteNumber("affectedCallers", callerN)
        writer.WriteBoolean("failOnImpure", failOnImpure)
        writer.WriteEndObject()

        writer.WriteStartArray "impureCalls"

        for d in calls do
            writer.WriteStartObject()
            writeString writer "caller" d.Caller
            writeString writer "callee" (if d.Callee <> "" then d.Callee else d.FullName)
            writeString writer "file" d.File
            writer.WriteNumber("startLine", d.StartLine)
            writer.WriteNumber("startColumn", d.StartColumn)
            writer.WriteNumber("endLine", d.EndLine)
            writer.WriteNumber("endColumn", d.EndColumn)
            writeString writer "message" d.Message
            writer.WriteEndObject()

        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()
        stream.ToArray()

    let writeSarif (project: string) (items: Diagnostic list) : byte[] =
        let items = Diagnostic.sort items
        let project = Paths.normalize project

        use stream = new MemoryStream()

        use writer =
            new Utf8JsonWriter(
                stream,
                JsonWriterOptions(Indented = false, SkipValidation = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping)
            )

        writer.WriteStartObject()
        writeString writer "$schema" "https://json.schemastore.org/sarif-2.1.0.json"
        writeString writer "version" "2.1.0"
        writer.WriteStartArray "runs"
        writer.WriteStartObject()

        writer.WriteStartObject "tool"
        writer.WriteStartObject "driver"
        writeString writer "name" Constants.ToolName
        writeString writer "version" Constants.ToolVersion
        writeString writer "informationUri" "https://github.com/e-St/fspure"
        writer.WriteStartArray "rules"

        writer.WriteStartObject()
        writeString writer "id" Constants.CallCode
        writeString writer "name" "ImpureCallInside"
        writer.WriteStartObject "shortDescription"
        writeString writer "text" "An impure function is called inside another function."
        writer.WriteEndObject()
        writer.WriteEndObject()

        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.WriteEndObject()

        if project <> "" then
            writer.WriteStartArray "artifacts"
            writer.WriteStartObject()
            writer.WriteStartObject "location"
            writeString writer "uri" project
            writer.WriteEndObject()
            writer.WriteEndObject()
            writer.WriteEndArray()

        writer.WriteStartArray "results"

        let calls = impureCallsOf items

        for d in calls do
            writer.WriteStartObject()
            writeString writer "ruleId" Constants.CallCode
            writeString writer "level" "note"
            writer.WriteStartObject "message"
            writeString writer "text" d.Message
            writer.WriteEndObject()
            writer.WriteStartArray "locations"
            writer.WriteStartObject()
            writer.WriteStartObject "physicalLocation"
            writer.WriteStartObject "artifactLocation"
            writeString writer "uri" d.File
            writer.WriteEndObject()
            writer.WriteStartObject "region"
            writer.WriteNumber("startLine", d.StartLine)
            writer.WriteNumber("startColumn", d.StartColumn)
            writer.WriteNumber("endLine", d.EndLine)
            writer.WriteNumber("endColumn", d.EndColumn)
            writer.WriteEndObject()
            writer.WriteEndObject()
            writer.WriteEndObject()
            writer.WriteEndArray()
            writer.WriteStartObject "properties"
            writeString writer "caller" d.Caller
            writeString writer "callee" (if d.Callee <> "" then d.Callee else d.FullName)
            writer.WriteEndObject()
            writer.WriteEndObject()

        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()
        stream.ToArray()

    let render (opts: AnalyzeOptions) (projectRel: string) (items: Diagnostic list) : byte[] =
        match opts.Format with
        | Json -> writeJson projectRel opts.Focus opts.Ignore opts.FailOnImpure items
        | Sarif -> writeSarif projectRel items

    let writeTo (pathOpt: string option) (bytes: byte[]) =
        match pathOpt with
        | None
        | Some ""
        | Some "-" ->
            Console.OpenStandardOutput().Write(bytes, 0, bytes.Length)
            Console.OpenStandardOutput().WriteByte(byte '\n')
        | Some path ->
            let full = Path.GetFullPath path
            match Path.GetDirectoryName full with
            | null
            | "" -> ()
            | dir -> Directory.CreateDirectory dir |> ignore

            File.WriteAllBytes(full, bytes)
            // Trailing newline so the file is a well-formed POSIX text file.
            File.AppendAllText(full, "\n", utf8NoBom)
