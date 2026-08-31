<!--
  GENERATED FILE — do not edit by hand.
  Template: src/docs/templates/customer.md.scriban
  Channel: preview | Ref: main | Version: 0.4.0
  Generated: 2026-08-31T20:38:39Z
-->

# Using fspure (get started)

You want pure/impure labels in the editor. You need **two** things:

1. **FSharp.PureAnalyzer** (NuGet) — classifies definitions  
2. **fsharp-pure-decorations** (VS Code extension) — shows **pure** / **impure** badges  

Plus **Ionide for F#**.


> Preview docs for `main`. Stable guide: [fspure.net](https://fspure.net) / [main README](https://github.com/e-St/fspure#traditional-setup).


---

## Fastest path: e-St/fstarter

Use [e-St/fstarter](https://github.com/e-St/fstarter) if you want a ready-made F# Codespace / dev container that **already includes fspure**. Open it, write F#, badges appear. No package wiring.

Use the steps below if you already have a repo and only want to add fspure.

---

## Install without a dev container

### 1. Analyzer

```bash
dotnet add package FSharp.PureAnalyzer --version 0.4.0
```

Paket:

```paket
nuget FSharp.PureAnalyzer 0.4.0
```

Point Ionide at a real folder. Easiest: copy `analyzers/dotnet/fs/` from the package into `./analyzers` in your workspace.

### 2. Extension

- Open VSX: **F# Pure Analyzer Decorations** (`e-st.fsharp-pure-decorations`)  
- Ionide: `ionide.ionide-fsharp`  
- Stock VS Code without Open VSX: install the `.vsix` from [GitHub Releases](https://github.com/e-St/fspure/releases)

### 3. Settings

Minimal `.vscode/settings.json`:

```json
{
  "FSharp.enableAnalyzers": true,
  "FSharp.analyzersPath": [
    "analyzers",
    "packages/Analyzers"
  ],
  "fsharpPureDecorations.enabled": true,
  "FSharp.lineLens.enabled": "replaceCodeLens",
  "FSharp.lineLens.prefix": "  // ",
  "workbench.colorCustomizations": {
    "editorHint.foreground": "#00000000",
    "editorHint.border": "#00000000",
    "editorOverviewRuler.hintForeground": "#00000000"
  }
}
```

Recommended extras (LineLens + hide grey diagnostic noise) — full Ionide block from our shared fragment:

```json
{
  "editor.inlineSuggest.enabled": false,
  "editor.parameterHints.enabled": false,
  "editor.acceptSuggestionOnEnter": "off",
  "[fsharp]": {
    "editor.quickSuggestions": false,
    "editor.suggestOnTriggerCharacters": false
  },
  "FSharp.enableMSBuildProjectGraph": true,
  "editor.inlayHints.enabled": "on",
  "FSharp.inlayHints.typeAnnotations": false,
  "FSharp.inlayHints.parameterNames": true,
  "FSharp.inlayHints.enabled": true,
  "FSharp.lineLens.enabled": "replaceCodeLens",
  "FSharp.lineLens.prefix": "  // ",
  "FSharp.pipelineHints.enabled": true,
  "FSharp.pipelineHints.prefix": "  // ",
  "FSharp.linter": true,
  "FSharp.enableAnalyzers": true,
  "FSharp.analyzersPath": [
    "/usr/local/share/fspure/analyzers",
    "analyzers",
    "packages/Analyzers"
  ],
  "FSharp.unusedDeclarationsAnalyzer": true,
  "FSharp.codeLenses.references.enabled": false,
  "editor.formatOnSave": true,
  "fsharpPureDecorations.enabled": true,
  "livePreview.defaultPreviewPath": "/.generated/site/index.html",
  "workbench.colorCustomizations": {
    "editorHint.foreground": "#00000000",
    "editorHint.border": "#00000000",
    "editorOverviewRuler.hintForeground": "#00000000"
  },
  "files.exclude": {
    "**/obj": true,
    "**/bin": true,
    "**/.paket": true
  },
  "chat.agent.sandbox.enabled": "on",
  "chat.tools.terminal.enableAutoApprove": true,
  "chat.tools.global.autoApprove": false,
  "chat.tools.terminal.blockDetectedFileWrites": "outsideWorkspace",
  "chat.tools.terminal.autoApprove": {
    "/^fspure(\\s|$)/": true,
    "/^dotnet\\s\u002B(build|test)\\b/": true,
    "/^dotnet\\s\u002Brun\\b.*\\sanalyze\\b/": true,
    "which": true,
    "command": true
  }
}
```

### 4. Check

Open any F# file. After Ionide loads you should see end-of-line badges.

Real pure/impure examples (from our e2e fixture):

```fsharp
// ---------------------------------------------------------------------------
// Misnamed helpers that look clean but call side effects — expect PURE002
// ---------------------------------------------------------------------------
let pureAdd (a: int) (b: int) =
    logSideEffect (sprintf "pureAdd %d %d" a b)
    mutateGlobal (a + b)
    a + b

let pureMultiply (a: int) (b: int) =
    logSideEffect (sprintf "pureMultiply %d %d" a b)
    let r = a * b
    mutateGlobal r
    r

let pureSquare (n: int) = pureMultiply n n

let pureProcessBatch (values: int list) =
    logSideEffect (sprintf "pureProcessBatch %d" values.Length)
    let mutable sum = 0
    for x in values do
        sum <- pureAdd sum x
    pureMultiply sum (getRandomImpure () % 3 + 1)

// ---------------------------------------------------------------------------
// Referentially transparent helpers — expect PURE003
// (Phase 2 pure-section screenshots must include add / isEmpty / myEmpty.)
// ---------------------------------------------------------------------------
let add a b =
    List.map (fun x -> x * a + b) [1; 2; 3]

let isEmpty = List.isEmpty

let myEmpty l =
    add 1 2 |> isEmpty

let double x = x * 2

let purePipeline (x: int) =
    x |> double |> fun n -> add n 0 |> List.sum
```

---

## Agentic Setup

<!-- <human id="skill-usage"> -->
The **fspure-reduce-impurity** skill teaches your coding agent to push side effects out of F# core logic. You describe what should stay pure; the agent runs `fspure analyze` and rewrites each impure call so the effect is passed in as a function argument.

The original I/O is not deleted. It moves to the boundary of the application.

#### Install the skill

**GitHub Copilot** (VS Code agent mode, Copilot CLI, or coding agent). Needs [GitHub CLI](https://cli.github.com/) 2.90+:

```text
gh skill install e-St/fspure fspure-reduce-impurity \
  --scope user \
  --pin main \
  --agent github-copilot
```

An [fspure](https://github.com/e-St/fspure) or [fstarter](https://github.com/e-St/fstarter) Codespace already does this on create. After the first official skill release you can pin a tag (`fspure-reduce-impurity-v*`) instead of `main`.

**Claude Code:**

```text
/plugin marketplace add e-St/fspure
/plugin install fspure@fspure
```

Then run `/fspure:fspure-reduce-impurity`, or just describe the task and let Claude pick the skill.

#### How to use it

1. Add the analyzer to the project so the agent can run `fspure analyze` (the Traditional Setup on this page, or [fspure.net/get-started](https://fspure.net/get-started.html)).
2. Point the agent at the code that should stay pure, for example:

   > Make `src/Core` purer. Ignore `src/Host`.

   You can also say “fix this PURE002” or “push I/O out of this function.”
3. The agent loops: build → `fspure analyze --fail-on-impure` → rewrite → repeat.
4. It is done when the report is clean, or when only a little impurity remains and it belongs at the edge of the app.

#### What a rewrite looks like

```fsharp
// before                         // after
let add x y =                     let printfHello s = printf "%s" s
    printf "hello"                let add write x y =
    x + y                             write "hello"
                                      x + y
```

The function keeps its name. Each effect becomes a parameter named for the **role** it plays (`write`, not `printf`). The original call is kept as a small example function you can pass in at the boundary. Tests can pass `ignore`.

CLI flags, JSON schema, and CI: [src/docs/AGENT.md](https://github.com/e-St/fspure/blob/main/src/docs/AGENT.md). The skill body is [plugins/fspure/skills/fspure-reduce-impurity/SKILL.md](https://github.com/e-St/fspure/blob/main/plugins/fspure/skills/fspure-reduce-impurity/SKILL.md).
<!-- </human> -->


---

## Library authors (one line)

Ship pure metadata inside your DLL so consumers get labels without re-running the collector:

```xml
    <!--
      THE ONE-LINER for library authors (also set in Directory.Build.props for the solution).
      PrivateAssets=all: do not flow the analyzer package to YOUR package consumers.
      Consumers who want pure/impure labels install FSharp.PureAnalyzer themselves ("fspure vanilla").
    -->
    <PackageReference Include="FSharp.PureAnalyzer" Version="$(FspureAnalyzerVersion)" PrivateAssets="all" />
```

See [src/samples/fspure-ready-lib](../src/samples/fspure-ready-lib/).

---

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| No badges | Is Ionide running? Is `FSharp.enableAnalyzers` true? |
| Analyzer not found | `FSharp.analyzersPath` must be a real directory (no `~`) |
| Only some files | Wait for project load; check FSAC output channel |
| Want to tweak pure list | Add `fspure.overrides.json` next to the `.fsproj` (see [ARCHITECTURE](ARCHITECTURE.md)) |

---

## Versions

- Analyzer: **0.4.0**  
- Collector: **0.1.0**  
- Docs channel: **preview** (`main`)
