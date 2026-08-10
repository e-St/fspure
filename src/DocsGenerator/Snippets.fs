namespace Fspure.DocsGenerator

open System
open System.IO
open System.Text.RegularExpressions

/// Extract named code samples from real source via markers.
///
/// Supported forms (line comments must match file language):
///   // <docs-snippet id="name"> … // </docs-snippet>
///   <!-- <docs-snippet id="name"> --> … <!-- </docs-snippet> -->
///   # <docs-snippet id="name"> … # </docs-snippet>
module Snippets =

    // Markers sit inside line comments: // …  # …  <!-- … -->
    // Open:  // <docs-snippet id="name">
    // Close: // </docs-snippet>
    let private openRe =
        Regex(
            """(?:^|\n)[ \t]*(?://|#|<!--)\s*<docs-snippet\s+id="([^"]+)"\s*/?\s*>\s*(?:-->)?\s*(?:\r?\n|$)""",
            RegexOptions.Compiled ||| RegexOptions.CultureInvariant
        )

    let private closeRe =
        Regex(
            """(?:^|\n)[ \t]*(?://|#|<!--)\s*</docs-snippet\s*>\s*(?:-->)?\s*(?:\r?\n|$)""",
            RegexOptions.Compiled ||| RegexOptions.CultureInvariant
        )

    type private Hit = { Id: string; BodyStart: int }

    /// Scan a single file; returns map id → body (no trailing blank line).
    let extractFromText (text: string) : Map<string, string> =
        let opens =
            openRe.Matches text
            |> Seq.cast<Match>
            |> Seq.map (fun m ->
                { Id = m.Groups[1].Value
                  BodyStart = m.Index + m.Length })
            |> Seq.toList

        let closes =
            closeRe.Matches text
            |> Seq.cast<Match>
            |> Seq.map (fun m -> m.Index)
            |> Seq.toList

        let rec pair (os: Hit list) (cs: int list) (acc: Map<string, string>) =
            match os with
            | [] -> acc
            | o :: restOs ->
                match cs |> List.tryFind (fun c -> c >= o.BodyStart) with
                | None -> pair restOs cs acc
                | Some c ->
                    let body =
                        text.Substring(o.BodyStart, c - o.BodyStart)
                        |> fun s ->
                            if s.StartsWith("\r\n", StringComparison.Ordinal) then s.Substring(2)
                            elif s.StartsWith("\n", StringComparison.Ordinal) then s.Substring(1)
                            else s
                        |> fun s -> s.TrimEnd()

                    let restCs = cs |> List.filter (fun x -> x <> c)
                    pair restOs restCs (Map.add o.Id body acc)

        pair opens closes Map.empty

    let private includeExt =
        set
            [ ".fs"
              ".fsproj"
              ".cs"
              ".csproj"
              ".json"
              ".xml"
              ".props"
              ".targets"
              ".sh"
              ".yml"
              ".yaml"
              ".md" ]

    let private skipDir =
        set
            [ "bin"
              "obj"
              "node_modules"
              ".git"
              "artifacts"
              "analyzers"
              "paket-files"
              "nupkgs" ]

    /// Walk the monorepo and collect all named snippets (first wins on collision with warning).
    let collect (repoRoot: string) : Map<string, string> * string list =
        let warnings = ResizeArray<string>()
        let mutable map = Map.empty

        let rec walk (dir: string) =
            for sub in Directory.EnumerateDirectories dir do
                let name = Path.GetFileName sub |> Option.ofObj |> Option.defaultValue ""

                if name <> "" && not (skipDir.Contains name) && not (name.StartsWith('.')) then
                    walk sub

            for file in Directory.EnumerateFiles dir do
                let ext =
                    Path.GetExtension file
                    |> Option.ofObj
                    |> Option.defaultValue ""
                    |> fun s -> s.ToLowerInvariant()

                if includeExt.Contains ext then
                    try
                        let text = File.ReadAllText file
                        let found = extractFromText text

                        let rel = Path.GetRelativePath(repoRoot, file).Replace('\\', '/')

                        for KeyValue(id, body) in found do
                            match Map.tryFind id map with
                            | Some _ ->
                                warnings.Add(
                                    $"duplicate docs-snippet id '{id}' in {rel} (ignored; first wins)"
                                )
                            | None -> map <- Map.add id body map
                    with ex ->
                        warnings.Add($"snippet scan failed for {file}: {ex.Message}")

        walk repoRoot
        map, List.ofSeq warnings
