namespace Fspure.DocsGenerator

open System
open System.IO
open System.Text.RegularExpressions

/// Hand-authored Markdown fragments that generators must not invent or reorder above.
///
/// Source of truth (committed):
///   src/docs/human/<id>.md
///
/// In Scriban templates:
///   {{ human "readme-top" }}
///
/// In generated output (for readability; re-loaded from disk next run):
///   <!-- <human id="readme-top"> -->
///   …body…
///   <!-- </human> -->
module Human =

    let private humanDir (repoRoot: string) =
        Path.Combine(repoRoot, "src", "docs", "human")

    let private blockOpen (id: string) =
        $"<!-- <human id=\"{id}\"> -->"

    let private blockClose = "<!-- </human> -->"

    /// Load all `src/docs/human/*.md` → id (filename without .md) → body.
    let loadAll (repoRoot: string) : Map<string, string> =
        let dir = humanDir repoRoot

        if not (Directory.Exists dir) then
            Map.empty
        else
            Directory.EnumerateFiles(dir, "*.md", SearchOption.TopDirectoryOnly)
            |> Seq.choose (fun path ->
                match Path.GetFileNameWithoutExtension path with
                | null
                | "" -> None
                | id when id.Equals("README", StringComparison.OrdinalIgnoreCase) ->
                    None // docs index, not a partial
                | id ->
                    let body = File.ReadAllText(path).TrimEnd() + "\n"
                    Some(id, body))
            |> Map.ofSeq

    /// Expand to a marked block so generated files stay greppable.
    let wrap (id: string) (body: string) : string =
        let body' = body.TrimEnd()
        $"{blockOpen id}\n{body'}\n{blockClose}\n"

    let get (humans: Map<string, string>) (id: string) : string =
        match Map.tryFind id humans with
        | Some body -> wrap id body
        | None ->
            failwith
                $"human id '{id}' not found. Add src/docs/human/{id}.md (hand-authored Markdown)."

    /// Optional: empty string if missing (for experimental partials).
    let tryGet (humans: Map<string, string>) (id: string) : string =
        match Map.tryFind id humans with
        | Some body -> wrap id body
        | None -> ""

    // Extract blocks from an existing generated file (migration / debug).
    let private openRe =
        Regex(
            """<!--\s*<human\s+id="([^"]+)"\s*>\s*-->""",
            RegexOptions.Compiled ||| RegexOptions.CultureInvariant
        )

    let private closeRe =
        Regex("""<!--\s*</human\s*>\s*-->""", RegexOptions.Compiled ||| RegexOptions.CultureInvariant)

    let extractFromGenerated (text: string) : Map<string, string> =
        let opens =
            openRe.Matches text
            |> Seq.cast<Match>
            |> Seq.map (fun m -> m.Groups[1].Value, m.Index + m.Length)
            |> Seq.toList

        opens
        |> List.choose (fun (id, bodyStart) ->
            let rest = text.Substring bodyStart
            let m = closeRe.Match rest

            if not m.Success then
                None
            else
                let body = rest.Substring(0, m.Index).TrimEnd() + "\n"
                Some(id, body))
        |> Map.ofList
