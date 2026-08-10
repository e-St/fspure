# Languages in this repository

**Default: F#.** Prefer F# for product code, tests, tools, and fixtures.

## Allowed exceptions (keep other languages only when necessary or clearly simpler)

| Area | Language | Why |
|------|----------|-----|
| **VS Code extension** (`editor/vscode-extension/`) | JavaScript | VS Code extension host API is JS/TS; no first-class F# extension host |
| **Playwright e2e** (`tests/e2e/phase2/playwright/`) | JavaScript | Playwright’s primary/node tooling surface |
| **MSBuild task** (`src/Fspure.BuildTasks/`) | **C#** | Task host uses `Activator.CreateInstance`; F# task types fail with MSB4061 (“Type must be a type provided by the runtime”) under out-of-proc execution |
| **Devcontainer generators** (`.devcontainer/generate*.py`) | Python | Small JSON-merge utilities used only at generate time; rewriting is optional |
| **YAML / shell** | YAML, bash | GitHub Actions and thin orchestration wrappers |
| **JSON / props** | data formats | Not “logic” languages |

## Product & tooling (F#)

| Area | Notes |
|------|--------|
| `src/FSharp.PureAnalyzer/` | Analyzer (F#) |
| `src/fspure-collector/` | dotnet tool (F#) |
| `src/FSharp.PureSchema/` | PureFile schema + PE reader (F#) |
| `src/Fspure.BuildTasks/` | MSBuild task **EmbedPureJson** (C# + Mono.Cecil — see exceptions) |
| `tests/e2e/phase1/AssertDefinitionBadges/` | SARIF baseline assert (F#) |
| `tests/fixtures/*` | PE resource fixture assemblies (F#) |
| `samples/fspure-ready-lib/` | Sample library (F#) |
| `scripts/release/` | Bash entrypoints calling `dotnet` / `gh` (orchestration only) |

## Rule of thumb for new code

1. Can this be a short **F# console / library** under `tools/` or next to the feature? → F#.  
2. Is it **VS Code** or **browser automation**? → JS is OK.  
3. Is it a **one-line shell** glue? → bash is OK.  
4. Otherwise default to **F#**.
