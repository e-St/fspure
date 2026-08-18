<!-- <human id="readme-top"> -->
<p align="center">
  <img src="src/docs/assets/fspure.png" alt="fspure logo" width="520" />
</p>

> Typically, interactions with the outside world occur at the boundary of your application.  
> — Isaac Abraham

# fspure

This project explores how an **F# analyzer** and **VS Code extension** can help you push impurity to the boundary of your application.

It does that by defining a pure subset and marking everything else as impure.

![pure / impure decorations in the editor](src/docs/assets/image.png)
<!-- </human> -->


<!--
  GENERATED below this line — do not hand-edit.
  Template: src/docs/templates/README.md.scriban
  Human prologue: src/docs/human/readme-top.md  (always first; never generated above)
  Channel: preview | Ref: main | Version: 0.4.0
  Generated: 2026-08-18T18:21:59Z
-->


> **Preview docs** for `main` (0.4.0).  
> Stable install guide always lives on [main / fspure.net](https://fspure.net).  
> This page: https://e-st.github.io/fspure/preview/main


## 60-second install

### 1. Analyzer (NuGet)

```bash
dotnet add package FSharp.PureAnalyzer --version 0.4.0
```

Paket:

```paket
nuget FSharp.PureAnalyzer 0.4.0
```

Copy the package’s `analyzers/dotnet/fs/` folder into a workspace folder named `analyzers` (Ionide needs a **real path** — no `~` or `${userHome}`).

### 2. VS Code extension

Install **F# Pure Analyzer Decorations** (`e-st.fsharp-pure-decorations`) from [Open VSX](https://open-vsx.org/extension/e-st/fsharp-pure-decorations), **and** [Ionide for F#](https://open-vsx.org/extension/Ionide/Ionide-fsharp).

```bash
# Extensions UI → search “F# Pure Analyzer Decorations” (Open VSX / VSCodium / code-server)
```

### 3. Workspace settings

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

Full end-user guide (dev containers, fstarter, troubleshooting): **[customer.md](customer.md)**.

---

<!-- <human id="skill-usage"> -->
## Using the fspure skill

The **fspure-reduce-impurity** skill teaches your coding agent to push side effects out of F# core logic. You describe what should stay pure; the agent runs `fspure analyze` and rewrites each impure call so the effect is passed in as a function argument.

The original I/O is not deleted. It moves to the boundary of the application.

### Install the skill

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

### How to use it

1. Add the analyzer to the project so the agent can run `fspure analyze` (the 60-second install on this page, or [fspure.net/get-started](https://fspure.net/get-started.html)).
2. Point the agent at the code that should stay pure, for example:

   > Make `src/Core` purer. Ignore `src/Host`.

   You can also say “fix this PURE002” or “push I/O out of this function.”
3. The agent loops: build → `fspure analyze --fail-on-impure` → rewrite → repeat.
4. It is done when the report is clean, or when only a little impurity remains and it belongs at the edge of the app.

### What a rewrite looks like

```fsharp
// before                         // after
let add x y =                     let printfHello s = printf "%s" s
    printf "hello"                let add write x y =
    x + y                             write "hello"
                                      x + y
```

The function keeps its name. Each effect becomes a parameter named for the **role** it plays (`write`, not `printf`). The original call is kept as a small example function you can pass in at the boundary. Tests can pass `ignore`.

CLI flags, JSON schema, and CI: [src/docs/AGENT.md](src/docs/AGENT.md). The skill body is [plugins/fspure/skills/fspure-reduce-impurity/SKILL.md](plugins/fspure/skills/fspure-reduce-impurity/SKILL.md).
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

---

## Real code, real labels

These snippets are **copied from this repo’s tests** at generate time (not hand-typed in the README).

### Pure vs impure (customer e2e fixture)

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

Source: `src/tests/e2e/customer-fixture/Program.fs`

### Library one-liner (embed pure.json in your DLL)

```xml
    <!--
      THE ONE-LINER for library authors (also set in Directory.Build.props for the solution).
      PrivateAssets=all: do not flow the analyzer package to YOUR package consumers.
      Consumers who want pure/impure labels install FSharp.PureAnalyzer themselves ("fspure vanilla").
    -->
    <PackageReference Include="FSharp.PureAnalyzer" Version="$(FspureAnalyzerVersion)" PrivateAssets="all" />
```

Source: `src/samples/fspure-ready-lib/src/Fspure.ReadyLib/Fspure.ReadyLib.fsproj`

```fsharp
    // --- Pure (collector should classify as pure) ---

    /// Integer addition.
    let add (x: int) (y: int) : int = x + y

    /// Integer multiplication.
    let mul (x: int) (y: int) : int = x * y

    /// Absolute value without branching on effects.
    let absInt (x: int) : int = if x < 0 then -x else x

    /// Clamp to an inclusive range (pure arithmetic / comparison).
    let clamp (lo: int) (hi: int) (x: int) : int =
        if x < lo then lo
        elif x > hi then hi
        else x

    /// Map a list of ints with a pure transformation (List.map is foundational pure).
    let mapDouble (xs: int list) : int list = List.map (fun n -> n * 2) xs

    /// Fold a sum (pure).
    let sum (xs: int list) : int = List.fold (fun acc n -> acc + n) 0 xs

    // --- Escape hatch (pure only via pure-extra.json merge) ---

    /// Intentionally not discoverable as pure by IL alone in all cases;
    /// pure-extra.json claims it so maintainers can see the merge path.
    let manualEscapeHatch (x: int) : int = x ^^^ 0

    // --- Impure (must remain impure) ---

    /// Side-effecting log — must NOT appear as pure in pure.json.
    let impureLog (message: string) : unit =
        System.Console.WriteLine(message)
```

Source: `src/samples/fspure-ready-lib/src/Fspure.ReadyLib/Library.fs`

More: **[src/samples/fspure-ready-lib](src/samples/fspure-ready-lib/)**.

---

## Links

- [Customer / install guide](customer.md)
- [Agent CLI](src/docs/AGENT.md)
- [Contributing / repo map](src/docs/CONTRIBUTING.md)
- [Releasing](src/docs/RELEASING.md)
- [Security](src/docs/SECURITY.md)
- [Product site](https://fspure.net)
- [NuGet: FSharp.PureAnalyzer](https://www.nuget.org/packages/FSharp.PureAnalyzer)
- [Open VSX extension](https://open-vsx.org/extension/e-st/fsharp-pure-decorations)

Versions in this build: analyzer **0.4.0**, collector **0.1.0**.
