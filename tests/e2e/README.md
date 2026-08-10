# Customer end-to-end tests

Automates a full **consumer-style** check of fspure:

1. PureAnalyzer classifies pure vs impure definitions.
2. The **fsharp-pure-decorations** VS Code extension shows `pure` / `impure` labels next to the code.

The suite is intentionally split into two phases.

## Phase 1 — Analyzer baseline

**Script:** `bash tests/e2e/phase1/run.sh`

| Step | Detail |
|------|--------|
| Build | `FSharp.PureAnalyzer` → `tests/e2e/.artifacts/analyzer-drop/` |
| Analyze | `fsharp-analyzers` on `tests/e2e/customer-fixture/Program.fs` |
| Assert | SARIF `PURE002`/`PURE003` vs baseline `tests/e2e/customer-fixture/expectations.json` |

The checked-in `expectations.json` is the **baseline of correct results** (captured from a known-good analyzer run). Regenerate only when a classification change is intentional:

```bash
UPDATE_BASELINE=1 bash tests/e2e/phase1/run.sh
# review git diff of expectations.json, then commit
```

Artifacts: `tests/e2e/.artifacts/phase1/`

## Phase 2 — Visual VS Code decorations

**Script:** `bash tests/e2e/phase2/run.sh`  
**Dev container:** `tests/e2e/phase2/.devcontainer/` (e2e-only; daily Codespaces use root `.devcontainer/` — “fspure IDE”)

| Step | Detail |
|------|--------|
| Prepare | Build analyzer into `customer-fixture/analyzers/`, pack decorations `.vsix` |
| VS Code | Start **code-server** (VS Code Web) with Ionide + the VSIX |
| Settings | Consumer-style Ionide + decoration settings (`customer-fixture/.vscode/settings.json`) |
| Solution | Load `customer-fixture.slnx` into Ionide |
| Open | `Program.fs` after the solution is loaded |
| Capture | Playwright screenshots → `tests/e2e/.artifacts/phase2/*.png` |

Screenshots are uploaded as the `phase2-visual-screenshots` workflow artifact for **human visual review**. The job also fails if both `pure` and `impure` decoration labels never appear in the editor within the wait window.

Consumer UI parity (Ionide settings used in Phase 2):

- `dotnet.defaultSolution` + `FSharp.workspacePath` → `customer-fixture.slnx`
- `FSharp.enableAnalyzers` + `FSharp.analyzersPath` → local `analyzers/` drop
- `FSharp.inlayHints.typeAnnotations: false` → no `a : int` on arguments
- `FSharp.inlayHints.parameterNames: true`
- `FSharp.lineLens.enabled: replaceCodeLens` + `prefix: "// "` → `// int -> int -> …` signatures
- `fsharpPureDecorations.*` → pure/impure **after** LineLens (`// signature pure`)
- transparent `editorHint.*` → hide grey diagnostic hint text so badges stay readable

### Local Phase 2 (optional)

```bash
# From fspure root, using the phase2 devcontainer definition:
devcontainer up --workspace-folder . --config tests/e2e/phase2/.devcontainer/devcontainer.json
devcontainer exec --workspace-folder . --config tests/e2e/phase2/.devcontainer/devcontainer.json \
  bash tests/e2e/phase2/run.sh
```

Or run `tests/e2e/phase2/run.sh` on any host that has .NET 10, Node 20, code-server, and Playwright deps.

## Library embed (Phase 4)

**Script:** `bash tests/e2e/ready-lib/run.sh` (same as `bash scripts/fspure-ready-lib-gate.sh`)

Local NuGet feed only: pack monorepo analyzer → pack `samples/fspure-ready-lib` → consumer + hard PURE002/PURE003 asserts. See [ready-lib/README.md](ready-lib/README.md). CI: `.github/workflows/fspure-ready-lib-gate.yml`.

## Phase 5 permanent regression

**Script:** `bash scripts/phase5-regression.sh`  
**CI:** `.github/workflows/phase5-regression.yml`

No extra library projects. Matrix covered with existing trees only:

| Slice | How |
|-------|-----|
| Foundational only | `tests/e2e/phase1` + `customer-fixture` |
| Foundational + ReadyLib (PackageReference) | `fspure-ready-lib-gate` + hard PURE00x |
| ProjectReference | same `Consumer` flag + analyse `ReadyLib` project for impureLog |
| Golden pure methods | `samples/fspure-ready-lib/tests/golden/` |
| Missing / zero / corrupt pure.json | unit tests (fallback clean) |
| VS Code badges (minimal) | `decorations.logic.test.js` (no IDE) |

`Consumer` impure wrappers are gated hard on **PackageReference** (realistic NuGet path). On **ProjectReference**, FCS may not wire cross-project callees; impure library surface is asserted by analysing `Fspure.ReadyLib.fsproj` directly.

## Layout

```
tests/e2e/
  customer-fixture/           # Mini consumer project
    Program.fs
    expectations.json         # Phase 1 baseline
    .vscode/settings.json     # Ionide + decorations settings for Phase 2
  phase1/
    run.sh
    AssertDefinitionBadges/
  phase2/
    .devcontainer/            # Image with code-server + Playwright deps
    prepare-workspace.sh
    start-code-server.sh
    run.sh
    playwright/
      package.json
      screenshot.mjs
  ready-lib/
    run.sh                    # Phase 4 library-embed gate (local feed)
    README.md
  README.md
```

## GitHub Actions

Workflow: `.github/workflows/e2e-customer.yml`

**Both phases** run inside `tests/e2e/phase2/.devcontainer` (`.NET` + Node + code-server + Playwright). They do **not** use the root **fspure IDE** `.devcontainer` or the **PureAnalyzer build** `src/FSharp.PureAnalyzer/.devcontainer/` container, so Codespaces/setup or pack/build changes cannot break visual e2e. See [`.devcontainer/README.md`](../.devcontainer/README.md).

```
phase1-analyzer  ──needs──►  phase2-visual
     │                            │
     ▼                            ▼
  phase1-analyzer          phase2-visual-screenshots
  (SARIF + report)         (PNG + meta + logs)
```

## Reviewing Phase 2 screenshots

After a CI run, download **phase2-visual-screenshots** and check:

| Badge | Example definitions |
|-------|---------------------|
| **impure** | `logSideEffect`, `pureAdd`, `pureProcessBatch`, `main` |
| **pure** | `add`, `isEmpty`, `myEmpty`, `double`, `purePipeline` |

The **`program-fs-pure-section.png`** artifact is scrolled to the transparent helpers block:

```fsharp
let add a b =
    List.map (fun x -> x * a + b) [1; 2; 3]

let isEmpty = List.isEmpty

let myEmpty l =
    add 1 2 |> isEmpty
```

Expected end-of-line layout:

```text
let add a b = // int -> int -> list<int> pure
```

(LineLens signature first, pure/impure badge after — not argument type inlays.)
