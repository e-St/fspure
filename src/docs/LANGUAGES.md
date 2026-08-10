# Languages in this repository

**Default: F#** for product code, tests, tooling, and monorepo tasks (Hindley–Milner / F# type inference, static checking).  
**Nix flakes** only for the optional SDK shell (`flake.nix`).  
**No new bash.** Remaining `.sh` files are thin `exec` shims or legacy (e2e / release / devcontainer host).

## Preferred stack

| Layer | Language | Role |
|-------|----------|------|
| Product + tools | **F#** | Analyzer, collector, embed, docs, gates, security audit |
| Monorepo tasks | **F#** (`src/Fspure.Tasks`) | `build`, `test`, `docs`, `security`, `ready-lib-gate`, `phase5` |
| Optional env | Nix flake | `dotnet` SDK on PATH via `nix develop` / direnv |
| CI orchestration | YAML | Calls `dotnet run --project …` (not bash logic) |

## Allowed exceptions

| Area | Language | Why |
|------|----------|-----|
| **VS Code host** | One file: `extension.js` | Host loads JS; **rules** live in F# (`Fspure.DecorationLogic`, xUnit). No separate `logic.js`. |
| **Site HTML/CSS** | Scriban → `.generated/` | Never commit `index.html` / `legal.html` / `privacy.html` / `site.css` — only templates under `src/docs/templates/site/` |
| **GitHub Actions** | YAML | Platform |
| **Legacy / host scripts** | thin bash | e2e (code-server), release publish, Codespaces lifecycle — **no new product logic** |
| **JSON / props / Scriban** | data / templates | Not application logic |

## F# tool map

| Project | Replaces / role |
|---------|-----------------|
| `src/Fspure.Tasks` | Monorepo CLI (was large scripts under `src/scripts/`) |
| `src/DocsGenerator` | Docs preview/stable (was `docs-generate.sh` body) |
| `src/DevcontainerGen` | Fragment merge |
| `src/FSharp.PureAnalyzer` | Analyzer |
| `src/fspure-collector` | pure.json collector |
| `src/Fspure.Embed` | Embed pure.json |
| `src/Fspure.DecorationLogic` | Badge rules |

## How to run tasks (F#)

```text
dotnet run --project src/Fspure.Tasks -- help
dotnet run --project src/Fspure.Tasks -- docs preview
dotnet run --project src/Fspure.Tasks -- security
dotnet run --project src/Fspure.Tasks -- ready-lib-gate
dotnet run --project src/Fspure.Tasks -- phase5
dotnet run --project src/Fspure.Tasks -- build
dotnet run --project src/Fspure.Tasks -- test
```

`src/scripts/*.sh` for those commands are **5-line shims** that only `exec` the F# tool (kept so old CI paths keep working).

## Rule of thumb

1. Logic → **F#**  
2. Optional toolchain pin → **Nix flake** (SDK only)  
3. No new shell scripts with branching / parsing / product rules  
4. No new Python, C#, or Dockerfiles for monorepo tooling  
