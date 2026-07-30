# Customer end-to-end tests

Automates the manual **consumer codespace check** (e.g. [skinow](https://github.com/e-St/skinow) `inaction/Program.fs`):

1. PureAnalyzer classifies pure vs impure definitions.
2. The **fsharp-pure-decorations** VS Code extension shows `pure` / `impure` labels next to the code.

The suite is intentionally split into two phases.

## Phase 1 — Analyzer baseline

**Script:** `bash e2e/phase1/run.sh`

| Step | Detail |
|------|--------|
| Build | `FSharp.PureAnalyzer` → `e2e/.artifacts/analyzer-drop/` |
| Analyze | `fsharp-analyzers` on `e2e/customer-fixture/Program.fs` |
| Assert | SARIF `PURE002`/`PURE003` vs baseline `e2e/customer-fixture/expectations.json` |

The checked-in `expectations.json` is the **baseline of correct results** (captured from a known-good analyzer run). Regenerate only when a classification change is intentional:

```bash
UPDATE_BASELINE=1 bash e2e/phase1/run.sh
# review git diff of expectations.json, then commit
```

Artifacts: `e2e/.artifacts/phase1/`

## Phase 2 — Visual VS Code decorations

**Script:** `bash e2e/phase2/run.sh`  
**Dev container:** `e2e/phase2/.devcontainer/`

| Step | Detail |
|------|--------|
| Prepare | Build analyzer into `customer-fixture/analyzers/`, pack decorations `.vsix` |
| VS Code | Start **code-server** (VS Code Web) with Ionide + the VSIX |
| Settings | Same Ionide / inlay / pure-decoration settings as the consumer codespace (`customer-fixture/.vscode/settings.json`) |
| Solution | Load `customer-fixture.slnx` into Ionide (same as manually picking the project’s `.slnx`) |
| Open | `Program.fs` after the solution is loaded |
| Capture | Playwright screenshots → `e2e/.artifacts/phase2/*.png` |

Screenshots are uploaded as the `phase2-visual-screenshots` workflow artifact for **human visual review** (Ionide type/parameter inlays + pure/impure labels next to the right definitions). The job also fails if both `pure` and `impure` decoration labels never appear in the editor within the wait window.

Consumer-codespace parity (settings mirrored from skinow-style `devcontainer.json`):

- `dotnet.defaultSolution` + `FSharp.workspacePath` → `customer-fixture.slnx` (Ionide must load a solution before analyzers/LineLens run)
- `FSharp.enableAnalyzers` + `FSharp.analyzersPath` → local `analyzers/` drop
- **skinow-parity Ionide UI:**
  - `FSharp.inlayHints.typeAnnotations: false` → no `a : int` on arguments
  - `FSharp.inlayHints.parameterNames: true`
  - `FSharp.lineLens.enabled: replaceCodeLens` + `prefix: "// "` → `// int -> int -> …` HM signatures
- `fsharpPureDecorations.*` → pure/impure **after** LineLens (`// signature pure`)
- transparent `editorHint.*` → hide grey diagnostic hint text so badges stay readable

### Local Phase 2 (optional)

```bash
# From fspure root, using the phase2 devcontainer definition:
devcontainer up --workspace-folder . --config e2e/phase2/.devcontainer/devcontainer.json
devcontainer exec --workspace-folder . --config e2e/phase2/.devcontainer/devcontainer.json \
  bash e2e/phase2/run.sh
```

Or run `e2e/phase2/run.sh` on any host that has .NET 10, Node 20, code-server, and Playwright deps.

## Layout

```
e2e/
  customer-fixture/           # Mini consumer project (skinow/inaction-style)
    Program.fs
    expectations.json         # Phase 1 baseline
    .vscode/settings.json     # Ionide + decorations settings for Phase 2
  phase1/
    run.sh
    assert-definition-badges.py
  phase2/
    .devcontainer/            # Image with code-server + Playwright deps
    prepare-workspace.sh
    start-code-server.sh
    run.sh
    playwright/
      package.json
      screenshot.mjs
  README.md
```

## GitHub Actions

Workflow: `.github/workflows/e2e-customer.yml`

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

Labels should appear at the **end of the definition line** (inlay hints after Ionide type annotations), not only in the Problems panel.
