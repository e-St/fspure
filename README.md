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

**Preferred (Nix flakes + direnv + F#):** see [src/docs/NIX.md](src/docs/NIX.md).

```text
direnv allow                    # once — loads flake (dotnet, nushell, fspure-docs, …)

nix run .#docs -- preview       # → .generated/site/preview/…
nix run .#docs -- stable 0.4.0  # → .generated/docs + .generated/site
nix run .#devcontainer          # fragments → .generated/devcontainer/

# or, with flake packages on PATH:
fspure-docs preview
fspure-devcontainer --check

# or plain F# (CI / no Nix):
dotnet run --project src/DocsGenerator -- preview
dotnet run --project src/DevcontainerGen
```

Interactive Nushell:

```text
nu
use src/scripts/fspure.nu *
fspure docs preview
```

Install and usage guides for end users live on **[fspure.net](https://fspure.net)** (generated from `src/docs/templates/`).

Maintainer docs (hand-authored):

- [src/docs/NIX.md](src/docs/NIX.md) — flakes / direnv / Nushell  
- [src/docs/DOCS.md](src/docs/DOCS.md) — docs generation & publish policy  
- [src/docs/RELEASING.md](src/docs/RELEASING.md) — release flow  
- [src/docs/PUBLISHING.md](src/docs/PUBLISHING.md) — package publishing  
- [src/docs/LANGUAGES.md](src/docs/LANGUAGES.md) — F# / Nix language policy  
