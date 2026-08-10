/// Phase 1: assert PureAnalyzer definition diagnostics match the baseline.
/// Maps diagnostic codes the same way the VS Code extension does for badges:
///   PURE002 -> impure
///   PURE003 -> pure
module AssertDefinitionBadges

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions

let private codeToBadge =
    Map.ofList
        [
            "PURE002", "impure"
            "PURE003", "pure"
        ]

let private funcRe =
    Regex(
        @"Function\s+'(?<name>[^']+)'\s+is\s+(?:not\s+)?transitively\s+pure",
        RegexOptions.IgnoreCase ||| RegexOptions.Compiled
    )

let private shortName (full: string) =
    match full.LastIndexOf '.' with
    | -1 -> full
    | i -> full.Substring(i + 1)

let private loadExpectations (path: string) : Map<string, string> =
    use doc = JsonDocument.Parse(File.ReadAllText path)
    let defs = doc.RootElement.GetProperty "definitions"
    let mutable m = Map.empty

    for p in defs.EnumerateObject() do
        let badge =
            match p.Value.GetString() with
            | null -> ""
            | s -> s

        if badge <> "pure" && badge <> "impure" then
            failwithf "Invalid badge '%s' for '%s'" badge p.Name

        m <- m.Add(p.Name, badge)

    if Map.isEmpty m then
        failwithf "No definitions in expectations file: %s" path

    m

let private extractDefinitionBadges (sarifPath: string) : Map<string, string> =
    use doc = JsonDocument.Parse(File.ReadAllText sarifPath)
    let mutable found = Map.empty

    for run in doc.RootElement.GetProperty("runs").EnumerateArray() do
        match run.TryGetProperty "results" with
        | false, _ -> ()
        | true, results ->
            for result in results.EnumerateArray() do
                let ruleId =
                    match result.TryGetProperty "ruleId" with
                    | true, v ->
                        match v.GetString() with
                        | null -> ""
                        | s -> s
                    | _ -> ""

                match Map.tryFind ruleId codeToBadge with
                | None -> ()
                | Some badge ->
                    let msg =
                        match result.TryGetProperty "message" with
                        | true, message ->
                            match message.TryGetProperty "text" with
                            | true, t ->
                                match t.GetString() with
                                | null -> ""
                                | s -> s
                            | _ -> message.ToString()
                        | _ -> ""

                    let m = funcRe.Match msg

                    if m.Success then
                        let name = shortName m.Groups["name"].Value

                        match Map.tryFind name found with
                        | Some "impure" -> ()
                        | _ when badge = "impure" || not (Map.containsKey name found) ->
                            found <- found.Add(name, badge)
                        | _ -> ()

    found

let private writeBaseline (path: string) (actual: Map<string, string>) =
    use stream = new MemoryStream()
    use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))
    writer.WriteStartObject()

    writer.WriteString(
        "$schema_comment",
        "Baseline definition badges for e2e/customer-fixture/Program.fs. PURE002 → impure, PURE003 → pure. Regenerate with: UPDATE_BASELINE=1 bash e2e/phase1/run.sh"
    )

    writer.WriteStartObject "definitions"

    for name, badge in actual |> Map.toList |> List.sortBy fst do
        writer.WriteString(name, badge)

    writer.WriteEndObject()
    writer.WriteStartArray "notes"
    writer.WriteStringValue "Badges in the editor are driven only by PURE002 (impure) and PURE003 (pure)."
    writer.WriteStringValue "PURE001 call-site hints are ignored by fsharp-pure-decorations for badge placement."
    writer.WriteStringValue "This fixture mixes misnamed pure* helpers (impure) with truly pure helpers (add/isEmpty/myEmpty)."
    writer.WriteEndArray()
    writer.WriteEndObject()
    writer.Flush()
    File.WriteAllBytes(path, stream.ToArray())
    File.AppendAllText(path, "\n")

[<EntryPoint>]
let main argv =
    let mutable sarif = None
    let mutable expectations = None
    let mutable writeBaselinePath = None
    let mutable writeReport = None
    let mutable allowExtra = false
    let mutable i = 0

    while i < argv.Length do
        match argv[i] with
        | "--sarif" when i + 1 < argv.Length ->
            sarif <- Some argv[i + 1]
            i <- i + 2
        | "--expectations" when i + 1 < argv.Length ->
            expectations <- Some argv[i + 1]
            i <- i + 2
        | "--write-baseline" when i + 1 < argv.Length ->
            writeBaselinePath <- Some argv[i + 1]
            i <- i + 2
        | "--write-report" when i + 1 < argv.Length ->
            writeReport <- Some argv[i + 1]
            i <- i + 2
        | "--allow-extra" ->
            allowExtra <- true
            i <- i + 1
        | other ->
            eprintfn "Unknown arg: %s" other
            i <- i + 1

    match sarif with
    | None ->
        eprintfn "usage: AssertDefinitionBadges --sarif <path> [--expectations <path>] [--write-baseline <path>] ..."
        2
    | Some sarifPath ->
        let actual = extractDefinitionBadges sarifPath

        match writeBaselinePath with
        | Some path ->
            writeBaseline path actual
            printfn "Wrote baseline with %d definitions → %s" (Map.count actual) path
            0
        | None ->
            match expectations with
            | None ->
                eprintfn "--expectations required unless --write-baseline"
                2
            | Some expPath ->
                let expected = loadExpectations expPath
                let lines = ResizeArray<string>()
                lines.Add "=== Expected (baseline) ==="

                for name, badge in expected |> Map.toList |> List.sortBy fst do
                    lines.Add(sprintf "  %-24s %s" name badge)

                lines.Add "=== Actual (analyzer) ==="

                for name, badge in actual |> Map.toList |> List.sortBy fst do
                    let mark =
                        match Map.tryFind name expected with
                        | Some b when b = badge -> "OK"
                        | None -> "??"
                        | _ -> "FAIL"

                    lines.Add(sprintf "  [%-4s] %-24s %s" mark name badge)

                let errors = ResizeArray<string>()

                for name, want in expected |> Map.toList do
                    if not (Map.containsKey name actual) then
                        errors.Add(sprintf "MISSING  %s: expected '%s'" name want)
                    else
                        let got = actual[name]

                        if got <> want then
                            errors.Add(sprintf "MISMATCH %s: expected '%s', got '%s'" name want got)

                if not allowExtra then
                    for name, got in actual |> Map.toList do
                        if not (Map.containsKey name expected) then
                            errors.Add(sprintf "UNEXPECTED %s: got '%s'" name got)

                let report = String.Join("\n", lines) + "\n"
                printf "%s" report

                match writeReport with
                | Some path ->
                    match Path.GetDirectoryName path with
                    | null
                    | "" -> ()
                    | d -> Directory.CreateDirectory d |> ignore

                    let body =
                        if errors.Count > 0 then
                            report + "\n=== FAILURES ===\n" + String.Join("\n", errors) + "\n"
                        else
                            report + "\nAll expected pure/impure definition badges matched.\n"

                    File.WriteAllText(path, body)
                | None -> ()

                if errors.Count > 0 then
                    eprintfn "\n=== FAILURES ==="

                    for e in errors do
                        eprintfn "%s" e

                    eprintfn
                        "\nPhase 1 baseline mismatch. If the new classification is intentional:\n  UPDATE_BASELINE=1 bash e2e/phase1/run.sh\nthen commit e2e/customer-fixture/expectations.json"

                    1
                else
                    printfn "\nAll expected pure/impure definition badges matched."
                    0
