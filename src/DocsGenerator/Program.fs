module Fspure.DocsGenerator.Program

open System
open System.IO
open Fspure.DocsGenerator

type private Args =
    {
        Root: string
        Channel: string
        RefName: string
        Version: string
        BaseUrl: string
        WriteMarkdown: bool
        SiteOut: string option
        MarkdownOut: string
        Templates: string
    }

let private usage () =
    """
fspure-docs — generate Markdown (and optional static site) from Scriban templates.

Usage:
  fspure-docs --root <repo> [options]

Options:
  --root PATH              Monorepo root (default: cwd)
  --channel stable|preview (default: preview)
  --ref NAME               Branch / tag / sha label (default: unknown)
  --version VER            Docs version label (default: lastOfficial analyzer)
  --base-url URL           Public base URL for this docs set
  --write-markdown         Write Markdown under --markdown-out (default: .generated/docs)
  --write-repo-files       Alias for --write-markdown (stable releases)
  --markdown-out PATH      Markdown output root (default: <root>/.generated/docs)
  --site-out PATH          Write static site files (HTML + assets) here
                           (default when omitted with --write-markdown: <root>/.generated/site)
  --templates PATH         Templates directory (default: <root>/src/docs/templates)

Examples:
  # PR / branch preview site (github.io only)
  fspure-docs --channel preview --ref feat-x \\
    --site-out .generated/site/preview/feat-x \\
    --base-url https://e-st.github.io/fspure/preview/feat-x

  # Official release: Markdown + site under .generated/ (not committed)
  fspure-docs --channel stable --ref v0.4.0 --version 0.4.0 --write-markdown \\
    --base-url https://fspure.net --site-out .generated/site
"""

let private parseArgs (argv: string[]) : Args =
    let mutable root = Directory.GetCurrentDirectory()
    let mutable channel = "preview"
    let mutable refName = "unknown"
    let mutable version = ""
    let mutable baseUrl = "https://fspure.net"
    let mutable writeMarkdown = false
    let mutable siteOut: string option = None
    let mutable markdownOut = ""
    let mutable templates = ""
    let mutable siteOutExplicit = false

    let rec loop i =
        if i >= argv.Length then
            ()
        else
            match argv[i] with
            | "--help"
            | "-h" ->
                printf "%s" (usage ())
                exit 0
            | "--root" when i + 1 < argv.Length ->
                root <- Path.GetFullPath argv[i + 1]
                loop (i + 2)
            | "--channel" when i + 1 < argv.Length ->
                channel <- argv[i + 1]
                loop (i + 2)
            | "--ref" when i + 1 < argv.Length ->
                refName <- argv[i + 1]
                loop (i + 2)
            | "--version" when i + 1 < argv.Length ->
                version <- argv[i + 1]
                loop (i + 2)
            | "--base-url" when i + 1 < argv.Length ->
                baseUrl <- argv[i + 1]
                loop (i + 2)
            | "--write-markdown"
            | "--write-repo-files" ->
                writeMarkdown <- true
                loop (i + 1)
            | "--markdown-out" when i + 1 < argv.Length ->
                markdownOut <- Path.GetFullPath argv[i + 1]
                loop (i + 2)
            | "--site-out" when i + 1 < argv.Length ->
                siteOut <- Some(Path.GetFullPath argv[i + 1])
                siteOutExplicit <- true
                loop (i + 2)
            | "--templates" when i + 1 < argv.Length ->
                templates <- Path.GetFullPath argv[i + 1]
                loop (i + 2)
            | other ->
                eprintfn "Unknown argument: %s" other
                eprintf "%s" (usage ())
                exit 2

    loop 0

    if String.IsNullOrWhiteSpace templates then
        templates <- Path.Combine(root, "src", "docs", "templates")

    if String.IsNullOrWhiteSpace markdownOut then
        markdownOut <- Path.Combine(root, ".generated", "docs")

    // Stable markdown runs also get a default site tree unless the caller set --site-out.
    if writeMarkdown && not siteOutExplicit && siteOut.IsNone then
        siteOut <- Some(Path.Combine(root, ".generated", "site"))

    {
        Root = root
        Channel = channel
        RefName = refName
        Version = version
        BaseUrl = baseUrl
        WriteMarkdown = writeMarkdown
        SiteOut = siteOut
        MarkdownOut = markdownOut
        Templates = templates
    }

[<EntryPoint>]
let main argv =
    try
        let args = parseArgs argv

        if args.WriteMarkdown && args.Channel <> "stable" then
            eprintfn "ERROR: --write-markdown requires --channel stable."
            exit 1

        printfn "fspure-docs"
        printfn "  root         = %s" args.Root
        printfn "  channel      = %s" args.Channel
        printfn "  ref          = %s" args.RefName
        printfn "  writeMarkdown= %b" args.WriteMarkdown
        printfn "  markdownOut  = %s" args.MarkdownOut
        printfn "  siteOut      = %s" (defaultArg (args.SiteOut |> Option.map string) "(none)")

        let model, warnings =
            Model.build args.Root args.Channel args.RefName args.Version args.BaseUrl args.WriteMarkdown

        for w in warnings do
            eprintfn "warning: %s" w

        printfn "  snippets  = %d" (Map.count model.Snippets)
        printfn "  version   = %s" model.Version

        let outputs = Render.renderAll args.Templates model
        printfn "  templates = %d" outputs.Length

        Render.writeOutputs
            args.Root
            args.MarkdownOut
            args.SiteOut
            args.WriteMarkdown
            outputs

        printfn "OK"
        0
    with ex ->
        eprintfn "ERROR: %s" ex.Message

        if not (isNull ex.StackTrace) then
            eprintfn "%s" ex.StackTrace

        1
