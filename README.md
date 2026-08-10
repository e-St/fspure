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
| `src/` | Hand-authored source: products, tests, samples, docs templates, scripts, fragments |
| `.generated/` | **Generated** outputs (gitignored) — docs site, merged markdown, devcontainer flavours |
| `.github/` | CI workflows |
| `.devcontainer/` | Codespaces / VS Code platform entry (materialized from fragments) |

```text
src/
  FSharp.PureAnalyzer/     # Roslyn/Ionide analyzer
  fspure-collector/        # purity collector tool
  Fspure.Embed/            # pure.json embed tool
  docs/                    # hand docs + Scriban templates + release manifests
  DocsGenerator/           # F# + Scriban docs generator
  devcontainer/fragments/  # devcontainer source of truth
  DevcontainerGen/         # merges fragments → .generated/devcontainer/
  tests/ samples/ editor/ scripts/ …
```

## Quick start

```bash
# Codespaces / Reopen in Container uses .devcontainer/devcontainer.json
# After changing fragments:
dotnet run --project src/DevcontainerGen

# Preview docs (writes under .generated/site/ only)
bash src/scripts/docs-generate.sh preview

# Stable docs + site for release publish (still under .generated/)
bash src/scripts/docs-generate.sh stable 0.4.0
```

Install and usage guides for end users live on **[fspure.net](https://fspure.net)** (generated from `src/docs/templates/`).

Maintainer docs (hand-authored):

- [src/docs/DOCS.md](src/docs/DOCS.md) — docs generation & publish policy  
- [src/docs/RELEASING.md](src/docs/RELEASING.md) — release flow  
- [src/docs/PUBLISHING.md](src/docs/PUBLISHING.md) — package publishing  
- [src/docs/LANGUAGES.md](src/docs/LANGUAGES.md) — F# / Nix language policy  
