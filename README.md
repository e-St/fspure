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
| `src/` | Hand-authored source: products, tests, samples, docs, **F# tools** |
| `src/Fspure.Tasks/` | **F# monorepo task runner** (replaces bash for docs/security/gates) |
| `.generated/` | Generated outputs (gitignored) |
| `.github/` | CI |
| `flake.nix` | Optional .NET SDK shell only |

## Quick start (F#)

```text
dotnet run --project src/Fspure.Tasks -- help
dotnet run --project src/Fspure.Tasks -- docs preview
dotnet run --project src/Fspure.Tasks -- security
dotnet run --project src/Fspure.Tasks -- ready-lib-gate
dotnet run --project src/DevcontainerGen
```

Install and usage for end users: **[fspure.net](https://fspure.net)**.

Maintainer docs:

- [src/docs/LANGUAGES.md](src/docs/LANGUAGES.md) — **F# first**, no new shell logic  
- [src/docs/DOCS.md](src/docs/DOCS.md) — docs publish policy  
- [src/docs/RELEASING.md](src/docs/RELEASING.md) — release flow  
- [src/docs/NIX.md](src/docs/NIX.md) — optional flake shell  
