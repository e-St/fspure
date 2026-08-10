<!--
  GENERATED FILE — do not edit by hand.
  Template: src/docs/templates/customer.md.scriban
  Channel: stable | Ref: v0.4.0 | Version: 0.4.0
  Generated: 2026-08-10T20:36:22Z
-->

# Using fspure (get started)

You want pure/impure labels in the editor. You need **two** things:

1. **FSharp.PureAnalyzer** (NuGet) — classifies definitions  
2. **fsharp-pure-decorations** (VS Code extension) — shows **pure** / **impure** badges  

Plus **Ionide for F#**.



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
    "analyzers",
    "packages/Analyzers"
  ],
  "FSharp.unusedDeclarationsAnalyzer": true,
  "FSharp.codeLenses.references.enabled": false,
  "editor.formatOnSave": true,
  "fsharpPureDecorations.enabled": true,
  "workbench.colorCustomizations": {
    "editorHint.foreground": "#00000000",
    "editorHint.border": "#00000000",
    "editorOverviewRuler.hintForeground": "#00000000"
  },
  "files.exclude": {
    "**/obj": true,
    "**/bin": true,
    "**/.paket": true
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
- Docs channel: **stable** (`v0.4.0`)
