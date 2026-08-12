namespace Fspure.Cli

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions

/// Read PURE002 / PURE003 results from an fsharp-analyzers SARIF document.
module SarifRead =

    let private funcRe =
        Regex(
            @"Function\s+'(?<name>[^']+)'\s+is\s+(?:not\s+)?transitively\s+pure",
            RegexOptions.IgnoreCase ||| RegexOptions.Compiled ||| RegexOptions.CultureInvariant
        )

    let private callRe =
        Regex(
            @"Call to '(?<callee>[^']+)'(?: inside '(?<caller>[^']+)')? is not known to be pure",
            RegexOptions.IgnoreCase ||| RegexOptions.Compiled ||| RegexOptions.CultureInvariant
        )

    let private str (el: JsonElement) =
        match el.ValueKind with
        | JsonValueKind.String ->
            match el.GetString() with
            | null -> ""
            | s -> s
        | _ -> ""

    let private tryProp (el: JsonElement) (name: string) =
        match el.TryGetProperty name with
        | true, v -> Some v
        | _ -> None

    let private intOr (el: JsonElement) (name: string) (fallback: int) =
        match tryProp el name with
        | Some v when v.ValueKind = JsonValueKind.Number ->
            let n = v.GetInt32()
            if n < 1 then fallback else n
        | _ -> fallback

    let fullNameFromMessage (message: string) : string =
        let m = funcRe.Match message

        if m.Success then
            m.Groups["name"].Value
        else
            let c = callRe.Match message
            if c.Success then c.Groups["callee"].Value else ""

    let callerFromMessage (message: string) : string =
        let c = callRe.Match message

        if c.Success then
            c.Groups["caller"].Value
        else
            ""

    let calleeFromMessage (message: string) : string =
        let c = callRe.Match message
        if c.Success then c.Groups["callee"].Value else ""

    let private readLocation (resolveDir: string) (relativeRoot: string) (result: JsonElement) =
        let mutable file = ""
        let mutable sl = 1
        let mutable sc = 1
        let mutable el = 1
        let mutable ec = 1

        match tryProp result "locations" with
        | Some locs when locs.ValueKind = JsonValueKind.Array && locs.GetArrayLength() > 0 ->
            let loc0 = locs[0]

            match tryProp loc0 "physicalLocation" with
            | Some phys ->
                match tryProp phys "artifactLocation" with
                | Some art ->
                    match tryProp art "uri" with
                    | Some u ->
                        let raw = str u
                        let decoded =
                            if raw.StartsWith("file:", StringComparison.OrdinalIgnoreCase) then
                                try
                                    Uri(raw).LocalPath
                                with _ ->
                                    raw
                            else
                                raw

                        let fileName =
                            match Path.GetFileName decoded with
                            | null
                            | "" -> decoded
                            | n -> n

                        let parentResolve =
                            match Path.GetDirectoryName resolveDir with
                            | null
                            | "" -> None
                            | p -> Some p

                        let candidates =
                            [
                                if Paths.isAbsolute decoded then
                                    Path.GetFullPath decoded
                                if not (String.IsNullOrWhiteSpace relativeRoot) then
                                    Path.GetFullPath(Path.Combine(relativeRoot, decoded))
                                if not (String.IsNullOrWhiteSpace resolveDir) then
                                    Path.GetFullPath(Path.Combine(resolveDir, decoded))
                                    Path.GetFullPath(Path.Combine(resolveDir, fileName))
                                match parentResolve with
                                | Some p -> Path.GetFullPath(Path.Combine(p, decoded))
                                | None -> ()
                            ]

                        let full =
                            match candidates |> List.tryFind File.Exists with
                            | Some p -> p
                            | None ->
                                match candidates with
                                | h :: _ -> h
                                | [] -> decoded

                        file <- Paths.relativeTo relativeRoot full
                    | None -> ()
                | None -> ()

                match tryProp phys "region" with
                | Some region ->
                    sl <- intOr region "startLine" 1
                    sc <- intOr region "startColumn" 1
                    el <- intOr region "endLine" sl
                    ec <- intOr region "endColumn" sc
                | None -> ()
            | None -> ()
        | _ -> ()

        file, sl, sc, el, ec

    let private messageText (result: JsonElement) =
        match tryProp result "message" with
        | None -> ""
        | Some msg ->
            match tryProp msg "text" with
            | Some t -> str t
            | None ->
                match msg.ValueKind with
                | JsonValueKind.String -> str msg
                | _ -> ""

    let private ruleId (result: JsonElement) =
        match tryProp result "ruleId" with
        | Some v -> str v
        | None -> ""

    let parseRel (resolveDir: string) (relativeRoot: string) (json: string) : Result<Diagnostic list, string> =
        try
            use doc = JsonDocument.Parse(json)
            let root = doc.RootElement
            let acc = ResizeArray<Diagnostic>()

            match tryProp root "runs" with
            | Some runs when runs.ValueKind = JsonValueKind.Array ->
                for run in runs.EnumerateArray() do
                    match tryProp run "results" with
                    | Some results when results.ValueKind = JsonValueKind.Array ->
                        for result in results.EnumerateArray() do
                            let code = ruleId result

                            if Constants.PurityCodes.Contains code then
                                let msg = messageText result
                                let file, sl, sc, eln, ec = readLocation resolveDir relativeRoot result
                                let name = fullNameFromMessage msg
                                let caller = callerFromMessage msg
                                let callee = calleeFromMessage msg

                                acc.Add(
                                    {
                                        Code = code
                                        File = file
                                        StartLine = sl
                                        StartColumn = sc
                                        EndLine = eln
                                        EndColumn = ec
                                        Message = msg
                                        FullName = name
                                        Caller = caller
                                        Callee = callee
                                    }
                                )
                    | _ -> ()
            | _ -> ()

            Ok(acc |> Seq.toList |> Diagnostic.sort)
        with ex ->
            Error $"invalid SARIF: {ex.Message}"

    let parse (root: string) (json: string) = parseRel root root json

    let loadRel (resolveDir: string) (relativeRoot: string) (path: string) : Result<Diagnostic list, string> =
        if String.IsNullOrWhiteSpace path then
            Error "empty SARIF path"
        elif not (File.Exists path) then
            Error $"SARIF file not found: {path}"
        else
            try
                parseRel resolveDir relativeRoot (File.ReadAllText path)
            with ex ->
                Error $"failed to read SARIF '{path}': {ex.Message}"

    let load (projectDir: string) (path: string) = loadRel projectDir projectDir path
