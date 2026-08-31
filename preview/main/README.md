<!-- <human id="readme-top"> -->
<p align="center">
  <img src="src/docs/assets/fspure.png" alt="fspure logo" width="520" />
</p>

> Typically, interactions with the outside world occur at the boundary of your application.  
> — Isaac Abraham

This project explores how an **F# analyzer** and **VS Code extension** can help you push impurity to the boundary of your application.

![pure / impure decorations in the editor](src/docs/assets/image.png)

## Why should I care?

Effects at the boundary leave you a core that is deterministic: same inputs, same outputs, no hidden I/O or mutation. That code is easier to test, review, and change. Getting there is not a single rewrite. It is a creative process — find an impure call, decide whether it belongs in the core, push it out, repeat. The analyzer reports what is still impure, the VS Code extension shows it on the function you are looking at, and an AI agent using the fspure skill can do the mechanical rewrites so you spend your time on those decisions.

## How does it work?

It does that by defining a pure subset and marking everything else as impure. The analyzer checks your F# code and labels each definition. The VS Code extension visualizes those labels as end-of-line **pure** / **impure** badges. An agent can use the same analyzer output to rewrite functions toward purity automatically. You can join the ecosystem by shipping purity information for your own libraries, instead of covering only F# core and the BCL.
<!-- </human> -->


<!--
  GENERATED below this line — do not hand-edit.
  Template: src/docs/templates/README.md.scriban
  Human prologue: src/docs/human/readme-top.md  (always first; never generated above)
  Channel: preview | Ref: main | Version: 0.4.0
  Generated: 2026-08-31T20:06:44Z
-->


> **Preview docs** for `main` (0.4.0).  
> Stable install guide always lives on [main / fspure.net](https://fspure.net).  
> This page: https://e-st.github.io/fspure/preview/main


## How can I use it?

You can wire the analyzer and badges into your editor yourself, or let an agent run the same loop from the skill. Both paths use the same purity labels.

### Traditional Setup

#### 1. Analyzer (NuGet)

```bash
dotnet add package FSharp.PureAnalyzer --version 0.4.0
```

Paket:

```paket
nuget FSharp.PureAnalyzer 0.4.0
```

Copy the package’s `analyzers/dotnet/fs/` folder into a workspace folder named `analyzers` (Ionide needs a **real path** — no `~` or `${userHome}`).

#### 2. VS Code extension

Install **F# Pure Analyzer Decorations** (`e-st.fsharp-pure-decorations`) from [Open VSX](https://open-vsx.org/extension/e-st/fsharp-pure-decorations), **and** [Ionide for F#](https://open-vsx.org/extension/Ionide/Ionide-fsharp).

```bash
# Extensions UI → search “F# Pure Analyzer Decorations” (Open VSX / VSCodium / code-server)
```

#### 3. Workspace settings

Paste into `.vscode/settings.json`:

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

Open an F# file, wait for Ionide. You should see:

- **pure** badges on clean functions  
- **impure** badges on anything that touches I/O, mutation, randomness, etc.

Full end-user guide (dev containers, fstarter, troubleshooting): **[get started](https://e-st.github.io/fspure/preview/main/get-started.html)**.

### Agentic Setup

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

## What you get

| Piece | What it does |
|-------|----------------|
| **FSharp.PureAnalyzer** | Marks definitions `PURE003` (pure) / `PURE002` (impure) |
| **fsharp-pure-decorations** | Turns those into end-of-line **pure** / **impure** badges |
| **fspure** | Agent/CI CLI: `fspure analyze --fail-on-impure` |
| **fspure-reduce-impurity** | Copilot / Claude skill that uses that CLI to push I/O to the boundary |
| **fspure-collector** (optional tool) | Builds pure-method lists from assemblies for libraries |

If a function only calls pure things, it is pure. If it (or anything it calls) does I/O or mutation, it is impure. You keep the messy stuff at the boundary of your app.

- [Real code, real labels](https://github.com/e-St/fspure/blob/main/src/docs/EXAMPLES.md)
- [Customer / install guide](https://e-st.github.io/fspure/preview/main/get-started.html)
- [Agent CLI](https://github.com/e-St/fspure/blob/main/src/docs/AGENT.md)
- [Contributing / repo map](https://github.com/e-St/fspure/blob/main/src/docs/CONTRIBUTING.md)
- [Releasing](https://github.com/e-St/fspure/blob/main/src/docs/RELEASING.md)
- [Security](https://github.com/e-St/fspure/blob/main/src/docs/SECURITY.md)
- [Product site](https://fspure.net)
- [NuGet: FSharp.PureAnalyzer](https://www.nuget.org/packages/FSharp.PureAnalyzer)
- [Open VSX extension](https://open-vsx.org/extension/e-st/fsharp-pure-decorations)

Versions in this build: analyzer **0.4.0**, collector **0.1.0**.
