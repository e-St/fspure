<!-- <human id="readme-top"> -->
# fspure

**See which F# functions are pure. Push the impure stuff to the edge.**

| | |
|---|---|
| **Product site** | [https://fspure.net](https://fspure.net) (updated only on official release) |
| **Doc previews** | [github.io/fspure/preview](https://e-st.github.io/fspure/preview/) |
| **Source** | Everything you edit lives under [`src/`](src/) |

## Layout

| Path | Role |
|------|------|
| `src/` | Hand-authored source: products, tests, samples, docs, **F# tools** |
| `src/Fspure.Tasks/` | **F# monorepo task runner** (replaces bash for docs/security/gates) |
| `.generated/` | Generated outputs (gitignored) |
| `.github/` | CI |
| `flake.nix` | Optional .NET SDK shell only |

## Quick start (F#)

```text
dotnet run --project src/Fspure.Tasks -- help
dotnet run --project src/Fspure.Tasks -- docs preview
dotnet run --project src/Fspure.Tasks -- security
dotnet run --project src/Fspure.Tasks -- ready-lib-gate
dotnet run --project src/Fspure.Tasks -- phase1
dotnet run --project src/Fspure.Tasks -- phase5
dotnet run --project src/DevcontainerGen
```

Install and usage for end users: **[fspure.net](https://fspure.net)**.

Maintainer docs:

- [src/docs/LANGUAGES.md](src/docs/LANGUAGES.md) — **F# first**, no new shell logic  
- [src/docs/DOCS.md](src/docs/DOCS.md) — docs publish policy  
- [src/docs/RELEASING.md](src/docs/RELEASING.md) — release flow  
- [src/docs/NIX.md](src/docs/NIX.md) — optional flake shell
<!-- </human> -->


<!--
  GENERATED below this line — do not hand-edit.
  Template: src/docs/templates/README.md.scriban
  Human prologue: src/docs/human/readme-top.md  (always first; never generated above)
  Channel: preview | Ref: human-check | Version: 0.4.0
  Generated: 2026-08-10T20:18:33Z
-->


> **Preview docs** for `human-check` (0.4.0).  
> Stable install guide always lives on [main / fspure.net](https://fspure.net).  
> This page: https://e-st.github.io/fspure/preview/human-check


---

## 60-second install

### 1. Analyzer (NuGet)

```bash
dotnet add package FSharp.PureAnalyzer --version 0.4.0
```

Paket:

```
nuget FSharp.PureAnalyzer 0.4.0
```

Copy the package’s `analyzers/dotnet/fs/` folder into a workspace folder named `analyzers` (Ionide needs a **real path** — no `~` or `${userHome}`).

### 2. VS Code extension

Install **F# Pure Analyzer Decorations** (`e-st.fsharp-pure-decorations`) from [Open VSX](https://open-vsx.org/extension/e-st/fsharp-pure-decorations), **and** [Ionide for F#](https://open-vsx.org/extension/Ionide/Ionide-fsharp).

```bash
# Open VSX / VSCodium / many code-server setups:
# Extensions UI → search “F# Pure Analyzer Decorations”
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

## What you get

| Piece | What it does |
|-------|----------------|
| **FSharp.PureAnalyzer** | Marks definitions `PURE003` (pure) / `PURE002` (impure) |
| **fsharp-pure-decorations** | Turns those into end-of-line **pure** / **impure** badges |
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

## Repo map

```text
src/              analyzer, schema, collector, embed, F# tools
src/Fspure.Tasks  monorepo CLI (docs, security, gates, phase1/5)
src/tests/        unit + e2e
src/samples/      fspure-ready-lib template
src/editor/       VS Code extension
src/docs/         hand docs, human/ partials, templates, releases
.generated/       docs site + markdown (gitignored)
```

---

## Docs channels

| Channel | When | Where |
|---------|------|--------|
| **Stable** | **Official release only** | **[fspure.net](https://fspure.net)** (+ generated site under `.generated/`) |
| **Preview** | Feature branches / beta tags | **[github.io](https://e-st.github.io/fspure/preview/)** only — not fspure.net |

Templates: `src/docs/templates/`. Human prose: `src/docs/human/`. Generator: `src/DocsGenerator`. Policy: [src/docs/DOCS.md](src/docs/DOCS.md).

```text
dotnet run --project src/Fspure.Tasks -- docs preview
dotnet run --project src/Fspure.Tasks -- docs stable 0.4.0
```

---

## Links

- [Customer / install guide](customer.md)
- [Releasing](src/docs/RELEASING.md)
- [Security](src/docs/SECURITY.md)
- [NuGet: FSharp.PureAnalyzer](https://www.nuget.org/packages/FSharp.PureAnalyzer)
- [Open VSX extension](https://open-vsx.org/extension/e-st/fsharp-pure-decorations)

Versions in this build: analyzer **0.4.0**, collector **0.1.0**.
