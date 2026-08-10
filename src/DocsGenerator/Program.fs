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
        WriteRepoFiles: bool
        SiteOut: string option
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
  --write-repo-files       Write README.md + docs/*.md into the repo tree
                           (stable releases only — do not use on everyday main pushes)
  --site-out PATH          Write static site files (HTML + assets) here
  --templates PATH         Templates directory (default: <root>/docs/templates)

Examples:
  # PR / branch preview site (no main-branch Markdown commits)
  fspure-docs --channel preview --ref feat-x --site-out _site/preview/feat-x \\
    --base-url https://fspure.net/preview/feat-x

  # Official release: update committed Markdown on main
  fspure-docs --channel stable --ref v0.4.0 --version 0.4.0 --write-repo-files \\
    --base-url https://fspure.net --site-out _site
"""

let private parseArgs (argv: string[]) : Args =
    let mutable root = Directory.GetCurrentDirectory()
    let mutable channel = "preview"
    let mutable refName = "unknown"
    let mutable version = ""
    let mutable baseUrl = "https://fspure.net"
    let mutable writeRepo = false
    let mutable siteOut: string option = None
    let mutable templates = ""

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
            | "--write-repo-files" ->
                writeRepo <- true
                loop (i + 1)
            | "--site-out" when i + 1 < argv.Length ->
                siteOut <- Some(Path.GetFullPath argv[i + 1])
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
        templates <- Path.Combine(root, "docs", "templates")

    {
        Root = root
        Channel = channel
        RefName = refName
        Version = version
        BaseUrl = baseUrl
        WriteRepoFiles = writeRepo
        SiteOut = siteOut
        Templates = templates
    }

[<EntryPoint>]
let main argv =
    try
        let args = parseArgs argv

        if args.WriteRepoFiles && args.Channel <> "stable" then
            eprintfn "ERROR: --write-repo-files requires --channel stable (main Markdown only on official releases)."
            exit 1

        printfn "fspure-docs"
        printfn "  root      = %s" args.Root
        printfn "  channel   = %s" args.Channel
        printfn "  ref       = %s" args.RefName
        printfn "  writeRepo = %b" args.WriteRepoFiles
        printfn "  siteOut   = %s" (defaultArg (args.SiteOut |> Option.map string) "(none)")

        let model, warnings =
            Model.build args.Root args.Channel args.RefName args.Version args.BaseUrl args.WriteRepoFiles

        for w in warnings do
            eprintfn "warning: %s" w

        printfn "  snippets  = %d" (Map.count model.Snippets)
        printfn "  version   = %s" model.Version

        let outputs = Render.renderAll args.Templates model
        printfn "  templates = %d" outputs.Length

        Render.writeOutputs args.Root args.SiteOut args.WriteRepoFiles outputs
        printfn "OK"
        0
    with ex ->
        eprintfn "ERROR: %s" ex.Message

        if not (isNull ex.StackTrace) then
            eprintfn "%s" ex.StackTrace

        1
