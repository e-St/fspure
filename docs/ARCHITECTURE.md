<!--
  GENERATED FILE — do not edit by hand.
  Template: docs/templates/ARCHITECTURE.md.scriban
  Channel: stable | Ref: v0.4.0 | Generated: 2026-08-10T18:48:04Z
-->

# fspure architecture (short)

How pure/impure labels are decided for vanilla fspure (analyzer + VS Code extension).

## Pieces

| Piece | Role |
|-------|------|
| **fspure-collector** | Scans assemblies (IL) → writes a `PureFile` (`.pure.json`) whitelist |
| **FSharp.PureAnalyzer** | Builds a pure set, labels defs (`PURE002` impure / `PURE003` pure) |
| **fsharp-pure-decorations** | End-of-line badges from those diagnostics |
| **MSBuild targets** (in the analyzer package) | After build: collect → optional `pure-extra.json` → embed `{AssemblyName}.pure.json` |

## Precedence (highest last)

```text
  foundational.pure.json
        │
        ▼
  + pure.json embeds from referenced DLLs
        │
        ▼
  ± fspure.overrides.json  (remove, then add)
```

**overrides > library embeds > foundational**

Disable foundational: `"useFoundational": false` in overrides, or `FSPURE_DISABLE_FOUNDATIONAL=1`.

## Library one-liner

```xml
    <!--
      THE ONE-LINER for library authors (also set in Directory.Build.props for the solution).
      PrivateAssets=all: do not flow the analyzer package to YOUR package consumers.
      Consumers who want pure/impure labels install FSharp.PureAnalyzer themselves ("fspure vanilla").
    -->
    <PackageReference Include="FSharp.PureAnalyzer" Version="$(FspureAnalyzerVersion)" PrivateAssets="all" />
```

`PrivateAssets=all` so your package consumers are not forced to take the analyzer.

Sample API (from the ready-lib):

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

## Consumers

1. Reference `FSharp.PureAnalyzer`  
2. Optional: VS Code extension  
3. Optional: `fspure.overrides.json` in the **app** project  

## TFM

Purity infrastructure targets **net10.0** only.

## Docs generation

Markdown is generated with **F# + Scriban** (`src/DocsGenerator`). Code samples use `<docs-snippet id="…">` markers in real source.  
Stable Markdown on `main` is refreshed **only on official releases**. Preview: `https://fspure.net`.
