module Fspure.DocsGenerator.Program

open System
open System.Diagnostics
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
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
fspure-docs — generate Markdown / static site from Scriban templates (F#).

High-level (preferred — no shell orchestration):
  fspure-docs preview [ref]              → .generated/site/preview/<ref>/  (github.io)
  fspure-docs stable [version]           → .generated/docs + .generated/site  (fspure.net)

Low-level flags (also accepted after preview|stable):
  --root PATH              Monorepo root (default: walk to fspure.slnx / cwd)
  --channel stable|preview
  --ref NAME
  --version VER
  --base-url URL
  --write-markdown         Write Markdown under --markdown-out
  --markdown-out PATH      default: <root>/.generated/docs
  --site-out PATH
  --templates PATH         default: <root>/src/docs/templates

Env (high-level modes):
  GH_PAGES_BASE   default https://e-st.github.io/fspure
  STABLE_BASE     default https://fspure.net
  CONFIGURATION   default Release (informational only when already built)
"""

let private findRepoRoot (start: string) : string =
    let rec walk d n =
        if n > 10 then start
        elif File.Exists(Path.Combine(d, "fspure.slnx")) then d
        else
            match Directory.GetParent d with
            | null -> start
            | p -> walk p.FullName (n + 1)

    walk (Path.GetFullPath start) 0

let private envOr (name: string) (fallback: string) =
    match Environment.GetEnvironmentVariable name with
    | null
    | "" -> fallback
    | v -> v

let private sanitizeRef (refName: string) : string =
    let s = refName.Replace('/', '-').Replace(' ', '-')
    Regex.Replace(s, @"[^A-Za-z0-9._-]", "")

let private tryGitBranch () : string option =
    try
        let psi =
            ProcessStartInfo(
                FileName = "git",
                Arguments = "rev-parse --abbrev-ref HEAD",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            )

        match Process.Start psi with
        | null -> None
        | p ->
            use _ = p
            let o = p.StandardOutput.ReadToEnd().Trim()
            p.WaitForExit(5_000) |> ignore

            if p.ExitCode = 0 && o <> "" && o <> "HEAD" then Some o else None
    with _ ->
        None

let private lastOfficialAnalyzer (root: string) : string =
    let path = Path.Combine(root, "src", "docs", "releases", "manifest.json")

    if not (File.Exists path) then
        "0.0.0"
    else
        try
            use doc = JsonDocument.Parse(File.ReadAllText path)

            doc.RootElement
                .GetProperty("lastOfficial")
                .GetProperty("FSharp.PureAnalyzer")
                .GetString()
            |> Option.ofObj
            |> Option.defaultValue "0.0.0"
        with _ ->
            "0.0.0"

/// Expand high-level `preview` / `stable` into full Args (logic formerly in docs-generate.sh).
let private expandHighLevel (root: string) (mode: string) (arg: string option) : Args =
    let ghBase = envOr "GH_PAGES_BASE" "https://e-st.github.io/fspure"
    let stableBase = envOr "STABLE_BASE" "https://fspure.net"

    match mode with
    | "preview" ->
        let refName =
            match arg with
            | Some r when r <> "" -> r
            | _ -> defaultArg (tryGitBranch ()) "local"

        let safe = sanitizeRef refName
        let site = Path.Combine(root, ".generated", "site", "preview", safe)
        let baseUrl = $"{ghBase.TrimEnd('/')}/preview/{safe}"

        printfn "==> preview docs for ref=%s → %s" refName site
        printfn "    public URL (github.io only): %s" baseUrl

        {
            Root = root
            Channel = "preview"
            RefName = refName
            Version = ""
            BaseUrl = baseUrl
            WriteMarkdown = false
            SiteOut = Some site
            MarkdownOut = Path.Combine(root, ".generated", "docs")
            Templates = Path.Combine(root, "src", "docs", "templates")
        }

    | "stable" ->
        let ver =
            match arg with
            | Some v when v <> "" -> v
            | _ -> lastOfficialAnalyzer root

        let site = Path.Combine(root, ".generated", "site")
        let md = Path.Combine(root, ".generated", "docs")

        printfn "==> stable docs version=%s → .generated/docs + .generated/site" ver
        printfn "    public URL (custom domain): %s" stableBase

        {
            Root = root
            Channel = "stable"
            RefName = $"v{ver}"
            Version = ver
            BaseUrl = stableBase
            WriteMarkdown = true
            SiteOut = Some site
            MarkdownOut = md
            Templates = Path.Combine(root, "src", "docs", "templates")
        }

    | other ->
        eprintfn "Unknown mode: %s (use preview | stable | or low-level flags)" other
        eprintf "%s" (usage ())
        exit 2

let private parseLowLevel (argv: string[]) (start: int) (root0: string) : Args =
    let mutable root = root0
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

    loop start

    root <- findRepoRoot root

    if String.IsNullOrWhiteSpace templates then
        templates <- Path.Combine(root, "src", "docs", "templates")

    if String.IsNullOrWhiteSpace markdownOut then
        markdownOut <- Path.Combine(root, ".generated", "docs")

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

let private parseArgs (argv: string[]) : Args =
    let root0 = findRepoRoot (Directory.GetCurrentDirectory())

    if argv.Length = 0 then
        expandHighLevel root0 "preview" None
    else
        match argv[0] with
        | "--help"
        | "-h" ->
            printf "%s" (usage ())
            exit 0
        | "preview"
        | "stable" as mode ->
            let arg =
                if argv.Length > 1 && not (argv[1].StartsWith("-", StringComparison.Ordinal)) then
                    Some argv[1]
                else
                    None

            // Allow extra flags after high-level mode; currently high-level wins.
            // (Low-level overrides can be added later if needed.)
            expandHighLevel root0 mode arg
        | _ when argv[0].StartsWith("-", StringComparison.Ordinal) -> parseLowLevel argv 0 root0
        | other ->
            eprintfn "Unknown command: %s" other
            eprintf "%s" (usage ())
            exit 2

let private run (args: Args) : int =
    if args.WriteMarkdown && args.Channel <> "stable" then
        eprintfn "ERROR: --write-markdown requires --channel stable."
        1
    else
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

        Render.writeOutputs args.Root args.MarkdownOut args.SiteOut args.WriteMarkdown outputs
        printfn "OK"
        0

[<EntryPoint>]
let main argv =
    try
        run (parseArgs argv)
    with ex ->
        eprintfn "ERROR: %s" ex.Message

        if not (isNull ex.StackTrace) then
            eprintfn "%s" ex.StackTrace

        1
