<!--
  GENERATED FILE — do not edit by hand.
  Template: src/docs/templates/README.md.scriban
  Generator: src/DocsGenerator (F# + Scriban)
  Channel: preview | Ref: nix-test | Version: 0.4.0
  Generated: 2026-08-10T19:41:08Z
  Snippets are pulled from real source via <docs-snippet id="…"> markers.
  Main-branch Markdown is only committed on stable releases.
-->

<p align="center">
  <img src="src/docs/assets/fspure.png" alt="fspure logo" width="520" />
</p>

> Typically, interactions with the outside world occur at the boundary of your application.  
> — Isaac Abraham

# fspure

**See which F# functions are pure. Push the impure stuff to the edge.**

Install one NuGet package + one VS Code extension. Open a file. Done.

![pure / impure decorations in the editor](src/docs/assets/image.png)


> **Preview docs** for `nix-test` (0.4.0).  
> Stable install guide always lives on [main / fspure.net](https://fspure.net).  
> This page: https://e-st.github.io/fspure/preview/nix-test


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

Full end-user guide (dev containers, fstarter, troubleshooting): **[docs/customer.md](docs/customer.md)**.

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
src/       analyzer, schema, collector, MSBuild tasks
tests/     unit + e2e
src/samples/   fspure-ready-lib template
src/editor/    VS Code extension
docs/      guides, templates, generated pages, assets
src/scripts/   CI / release helpers
```

---

## Docs channels

| Channel | When | Where |
|---------|------|--------|
| **Stable** | **Official release only** | This README on `main` + **[fspure.net](https://fspure.net)** |
| **Preview** | Feature branches / beta tags | **[github.io](https://e-st.github.io/fspure/preview/)** only — not fspure.net |

Templates: `src/docs/templates/`. Generator: `src/DocsGenerator` (F# + Scriban). Policy: [src/docs/DOCS.md](src/docs/DOCS.md).

```text
# Preview → github.io (does not rewrite main Markdown, does not touch fspure.net)
nix run .#docs -- preview
# or:  dotnet run --project src/DocsGenerator -- preview

# Stable → used by Official release only (.generated/ + fspure.net publish)
nix run .#docs -- stable 0.4.0
```

---

## Links

- [Customer / install guide](docs/customer.md)
- [Architecture](src/docs/ARCHITECTURE.md)
- [Releasing](src/docs/RELEASING.md)
- [Security](src/docs/SECURITY.md)
- [NuGet: FSharp.PureAnalyzer](https://www.nuget.org/packages/FSharp.PureAnalyzer)
- [Open VSX extension](https://open-vsx.org/extension/e-st/fsharp-pure-decorations)

Versions in this build: analyzer **0.4.0**, collector **0.1.0**.
