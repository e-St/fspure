# Languages in this repository

**Defaults: F# and Nix.** Prefer F# for product code, tests, tools, and e2e. Prefer flakes + direnv + Nushell for the developer environment.

## Allowed exceptions

| Area | Language | Why |
|------|----------|-----|
| **VS Code host shim** (`src/editor/vscode-extension/src/extension.js`) | JavaScript | VS Code loads JS/TS; decoration *rules* are F# (`src/Fspure.DecorationLogic`) |
| **GitHub Actions** | YAML | CI platform |
| **Nix `writeShellApplication`** | tiny `/bin/sh` | Only packaging glue: find repo root + `exec` F# tool — **no product logic** |
| **Legacy `src/scripts/*.sh`** | bash | Being retired; do not add new ones |
| **JSON / props / Scriban** | data / templates | Not application logic |

## F#

| Area | Notes |
|------|--------|
| `src/FSharp.PureAnalyzer/` | Analyzer |
| `src/fspure-collector/` | pure.json collector |
| `src/FSharp.PureSchema/` | Schema + PE reader |
| `src/Fspure.Embed/` | Embed pure.json (`dotnet exec`) |
| `src/Fspure.DecorationLogic/` | Pure/impure badge rules (extension + tests) |
| `src/DocsGenerator/` | Docs + site generation (**includes** preview/stable orchestration) |
| `src/DevcontainerGen/` | Devcontainer fragment merge |
| `src/tests/e2e/phase2/ScreenshotCapture/` | Visual e2e (**Playwright.NET**, F#) |

## Nix + interactive shell

| Piece | Notes |
|-------|--------|
| `flake.nix` | Flakes: `packages`, `apps`, `devShells` |
| `.envrc` | direnv + nix-direnv → `use flake` |
| `nushell` | Preferred interactive shell in the devShell (on PATH, not forced as `$SHELL`) |
| `src/scripts/fspure.nu` | Thin interactive helpers |

Full guide: [NIX.md](NIX.md).

## Rule of thumb

1. Logic → **F#**  
2. Toolchain / env → **Nix flakes** (+ direnv)  
3. Interactive glue → **Nushell**  
4. VS Code host edge → minimal JS only  
5. **No new bash scripts** (only accidental leftovers inside `writeShellApplication`)  
6. No new Python, C#, or Dockerfiles for monorepo tooling  
