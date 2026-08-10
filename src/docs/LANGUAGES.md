# Languages in this repository

**Defaults: F# and Nix.** Prefer F# for product code, tests, tools, and e2e.

## Allowed exceptions

| Area | Language | Why |
|------|----------|-----|
| **VS Code host shim** (`src/editor/vscode-extension/src/extension.js`) | JavaScript | VS Code loads JS/TS; decoration *rules* are F# (`src/Fspure.DecorationLogic`) |
| **YAML / shell** | YAML, bash | GitHub Actions and thin orchestration |
| **JSON / props / Scriban** | data / templates | Not application logic |

## F#

| Area | Notes |
|------|--------|
| `src/FSharp.PureAnalyzer/` | Analyzer |
| `src/fspure-collector/` | pure.json collector |
| `src/FSharp.PureSchema/` | Schema + PE reader |
| `src/Fspure.Embed/` | Embed pure.json (`dotnet exec`) |
| `src/Fspure.DecorationLogic/` | Pure/impure badge rules (extension + tests) |
| `src/DocsGenerator/`, `src/DevcontainerGen/` | Docs + devcontainer merge |
| `src/tests/e2e/phase2/ScreenshotCapture/` | Visual e2e (**Playwright.NET**, F#) |

## Nix

`flake.nix` — `nix develop` for SDK / Node (packaging only) / jq.

## Rule of thumb

1. Logic → **F#**  
2. Toolchain → **Nix**  
3. VS Code host edge → minimal JS only  
4. No new Python, C#, Dockerfiles, or Node app logic  
