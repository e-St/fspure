# Languages in this repository

**Defaults: F# and Nix.** Prefer F# for product code, tests, and tools. Prefer Nix for reproducible dev shells.

## Allowed exceptions

| Area | Language | Why |
|------|----------|-----|
| **VS Code extension** (`editor/vscode-extension/`) | JavaScript | VS Code extension host is JS/TS |
| **Playwright e2e** (`tests/e2e/phase2/playwright/`) | JavaScript | Playwright’s Node tooling surface |
| **YAML / shell** | YAML, bash | GitHub Actions and thin orchestration |
| **JSON / props / Scriban** | data / templates | Not application logic |

## F# product & tools

| Area | Notes |
|------|--------|
| `src/FSharp.PureAnalyzer/` | Analyzer |
| `src/fspure-collector/` | pure.json collector (dotnet tool) |
| `src/FSharp.PureSchema/` | PureFile schema + PE reader |
| `src/Fspure.Embed/` | Embed pure.json into DLLs (`dotnet exec`, used from MSBuild) |
| `src/DocsGenerator/` | Markdown / site (Scriban) |
| `src/DevcontainerGen/` | Merge devcontainer fragments (replaces former Python) |
| `tests/`, `samples/` | Tests and ready-lib sample |

## Nix

| File | Role |
|------|------|
| `flake.nix` | Dev shell: .NET SDK, Node, jq, git |

```bash
nix develop          # enter shell
```

Dev Containers use published images (`ghcr.io/e-st/fstarter`) plus **shell** post-create scripts — **no Dockerfiles in this repo**.

## Rule of thumb

1. Logic → **F#**  
2. Reproducible toolchain → **Nix**  
3. Editor host / Playwright → **JS** only when required  
4. Avoid new Python, C#, or Dockerfiles in-tree  
