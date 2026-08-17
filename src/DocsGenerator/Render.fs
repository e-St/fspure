namespace Fspure.DocsGenerator

open System
open System.IO
open System.Text.RegularExpressions
open Scriban
open Scriban.Runtime

module Render =

    let private scriptObject (model: DocsModel) : ScriptObject =
        let so = ScriptObject()

        so.SetValue("Channel", model.Channel, true)
        so.SetValue("RefName", model.RefName, true)
        so.SetValue("Version", model.Version, true)
        so.SetValue("GeneratedAt", model.GeneratedAt, true)
        so.SetValue("IsStableRelease", model.IsStableRelease, true)
        so.SetValue("BaseUrl", model.BaseUrl, true)
        so.SetValue("RepoRoot", model.RepoRoot, true)
        so.SetValue("AnalyzerVersion", model.AnalyzerVersion, true)
        so.SetValue("CollectorVersion", model.CollectorVersion, true)
        so.SetValue("WorkspaceSettingsJson", model.WorkspaceSettingsJson, true)
        so.SetValue("MinimalSettingsJson", model.MinimalSettingsJson, true)

        let snip (id: string) =
            match Map.tryFind id model.Snippets with
            | Some body -> body
            | None ->
                failwith
                    $"docs-snippet id '{id}' not found. Mark source with <docs-snippet id=\"{id}\"> … </docs-snippet>."

        // Human partials: always from src/docs/human/<id>.md — never invented by templates.
        let human (id: string) = Human.get model.Humans id
        let humanOpt (id: string) = Human.tryGet model.Humans id

        so.Import("snip", Func<string, string>(snip))
        so.Import("snippet", Func<string, string>(snip))
        so.Import("human", Func<string, string>(human))
        so.Import("human_opt", Func<string, string>(humanOpt))
        so

    let renderTemplate (templateText: string) (model: DocsModel) : string =
        let template = Template.Parse(templateText)

        if template.HasErrors then
            let msgs =
                template.Messages |> Seq.map (fun m -> m.ToString()) |> String.concat "\n"

            failwith $"Scriban parse errors:\n{msgs}"

        let ctx = TemplateContext()
        ctx.PushGlobal(scriptObject model)
        let result = template.Render ctx
        result.TrimEnd() + "\n"

    type OutputFile =
        {
            RelativePath: string
            Content: string
        }

    /// Strip the volatile generate timestamp so committed README diffs stay stable.
    let normalizeRepoMarkdown (text: string) : string =
        let re =
            Regex(@"^(\s*Generated:\s*)\d{4}-\d{2}-\d{2}T[^\r\n]*$", RegexOptions.Multiline)

        re.Replace(text, "${1}(synced)").TrimEnd() + "\n"

    let tryFindOutput (outputs: OutputFile list) (relativePath: string) : OutputFile option =
        outputs
        |> List.tryFind (fun o -> o.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase))

    let repoReadmePath (repoRoot: string) = Path.Combine(repoRoot, "README.md")

    /// Write the generated product README to the repo root (GitHub landing page).
    let writeRepoReadme (repoRoot: string) (content: string) : string =
        let dest = repoReadmePath repoRoot
        let body = normalizeRepoMarkdown content
        File.WriteAllText(dest, body)
        dest

    /// True when the committed root README matches the generated body (timestamps ignored).
    let repoReadmeMatches (repoRoot: string) (content: string) : bool =
        let expected = normalizeRepoMarkdown content
        let path = repoReadmePath repoRoot

        if not (File.Exists path) then
            false
        else
            normalizeRepoMarkdown (File.ReadAllText path) = expected

    let renderAll (templatesDir: string) (model: DocsModel) : OutputFile list =
        if not (Directory.Exists templatesDir) then
            failwith $"templates directory missing: {templatesDir}"

        Directory.EnumerateFiles(templatesDir, "*.scriban", SearchOption.AllDirectories)
        |> Seq.map (fun path ->
            let rel = Path.GetRelativePath(templatesDir, path).Replace('\\', '/')

            let outName =
                if rel.EndsWith(".scriban", StringComparison.OrdinalIgnoreCase) then
                    rel.Substring(0, rel.Length - ".scriban".Length)
                else
                    rel

            let content = renderTemplate (File.ReadAllText path) model

            {
                RelativePath = outName
                Content = content
            })
        |> Seq.toList

    let writeOutputs
        (repoRoot: string)
        (markdownOut: string)
        (siteOut: string option)
        (writeMarkdown: bool)
        (outputs: OutputFile list)
        : unit =
        let write (root: string) (rel: string) (content: string) =
            let dest = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar))

            match Path.GetDirectoryName dest |> Option.ofObj with
            | Some dir when dir <> "" -> Directory.CreateDirectory dir |> ignore
            | _ -> ()

            File.WriteAllText(dest, content)

            printfn "  wrote %s" (Path.GetRelativePath(repoRoot, dest).Replace('\\', '/'))

        for o in outputs do
            let name = o.RelativePath

            if name.StartsWith("site/", StringComparison.Ordinal) then
                match siteOut with
                | Some siteRoot -> write siteRoot (name.Substring("site/".Length)) o.Content
                | None -> ()
            else
                if writeMarkdown then
                    // Templates may be "README.md", "docs/foo.md", or bare "foo.md".
                    let destRel =
                        if name.Equals("README.md", StringComparison.OrdinalIgnoreCase) then
                            "README.md"
                        elif name.StartsWith("docs/", StringComparison.Ordinal) then
                            name.Substring("docs/".Length)
                        else
                            name

                    write markdownOut destRel o.Content

                // Mirror Markdown into the static site (preview or stable).
                match siteOut with
                | Some siteRoot -> write siteRoot name o.Content
                | None -> ()

        match siteOut with
        | None -> ()
        | Some siteRoot ->
            let docsSrc = Path.Combine(repoRoot, "src", "docs")

            // Binary / non-template assets only. HTML+CSS come from templates/site/*.scriban.
            let staticPairs =
                [
                    Path.Combine(docsSrc, "assets", "fspure.png"), "fspure.png"
                    Path.Combine(docsSrc, "assets", "fspure.png"), Path.Combine("assets", "fspure.png")
                    Path.Combine(docsSrc, "assets", "image.png"), Path.Combine("assets", "image.png")
                    Path.Combine(docsSrc, ".nojekyll"), ".nojekyll"
                ]

            // Only ship CNAME at stable site root (not under /preview/…)
            let withCname =
                if siteRoot.Replace('\\', '/').Contains("/preview/") then
                    staticPairs
                else
                    (Path.Combine(docsSrc, "CNAME"), "CNAME") :: staticPairs

            for src, destRel in withCname do
                if File.Exists src then
                    let dest = Path.Combine(siteRoot, destRel.Replace('/', Path.DirectorySeparatorChar))

                    match Path.GetDirectoryName dest |> Option.ofObj with
                    | Some dir when dir <> "" -> Directory.CreateDirectory dir |> ignore
                    | _ -> ()

                    File.Copy(src, dest, true)
                    printfn "  copied %s → %s" (Path.GetRelativePath(repoRoot, src).Replace('\\', '/')) destRel
