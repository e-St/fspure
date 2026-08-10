namespace Fspure.DocsGenerator

open System
open System.IO
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

        so.Import("snip", Func<string, string>(snip))
        so.Import("snippet", Func<string, string>(snip))
        so

    let renderTemplate (templateText: string) (model: DocsModel) : string =
        let template = Template.Parse(templateText)

        if template.HasErrors then
            let msgs =
                template.Messages
                |> Seq.map (fun m -> m.ToString())
                |> String.concat "\n"

            failwith $"Scriban parse errors:\n{msgs}"

        let ctx = TemplateContext()
        ctx.PushGlobal(scriptObject model)
        let result = template.Render ctx
        result.TrimEnd() + "\n"

    type OutputFile = { RelativePath: string; Content: string }

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
            { RelativePath = outName; Content = content })
        |> Seq.toList

    let writeOutputs
        (repoRoot: string)
        (siteOut: string option)
        (writeRepoFiles: bool)
        (outputs: OutputFile list)
        : unit
        =
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
                if writeRepoFiles then
                    let destRel =
                        if name.Equals("README.md", StringComparison.OrdinalIgnoreCase) then
                            "README.md"
                        elif name.StartsWith("docs/", StringComparison.Ordinal) then
                            name
                        else
                            Path.Combine("docs", name).Replace('\\', '/')

                    write repoRoot destRel o.Content

                // Mirror Markdown into the static site (preview or stable).
                match siteOut with
                | Some siteRoot -> write siteRoot name o.Content
                | None -> ()

        match siteOut with
        | None -> ()
        | Some siteRoot ->
            let staticPairs =
                [
                    "docs/assets/fspure.png", "fspure.png"
                    "docs/assets/fspure.png", "assets/fspure.png"
                    "docs/assets/image.png", "assets/image.png"
                    "docs/site.css", "site.css"
                    "docs/legal.html", "legal.html"
                    "docs/privacy.html", "privacy.html"
                    "docs/.nojekyll", ".nojekyll"
                ]

            // Only ship CNAME at stable site root (not under /preview/…)
            let withCname =
                if siteRoot.Replace('\\', '/').Contains("/preview/") then
                    staticPairs
                else
                    ("docs/CNAME", "CNAME") :: staticPairs

            for srcRel, destRel in withCname do
                let src = Path.Combine(repoRoot, srcRel.Replace('/', Path.DirectorySeparatorChar))

                if File.Exists src then
                    let dest =
                        Path.Combine(siteRoot, destRel.Replace('/', Path.DirectorySeparatorChar))

                    match Path.GetDirectoryName dest |> Option.ofObj with
                    | Some dir when dir <> "" -> Directory.CreateDirectory dir |> ignore
                    | _ -> ()

                    File.Copy(src, dest, true)
