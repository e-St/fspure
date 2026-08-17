<p align="center">
  <img src="src/docs/assets/fspure.png" alt="fspure logo" width="520" />
</p>

> Typically, interactions with the outside world occur at the boundary of your application.  
> — Isaac Abraham

# fspure

This project explores how an **F# analyzer** and **VS Code extension** can help you push impurity to the boundary of your application.

It does that by defining a pure subset and marking everything else as impure.

![pure / impure decorations in the editor](src/docs/assets/image.png)

| Component | Role |
|-----------|------|
| **FSharp.PureAnalyzer** | Classifies definitions (`PURE002` impure / `PURE003` pure) for Ionide & `fsharp-analyzers` |
| **fspure** | Agent CLI: `fspure analyze --fail-on-impure` → deterministic JSON/SARIF ([AGENT.md](src/docs/AGENT.md)) |
| **fspure-reduce-impurity** | Copilot / Claude skill that uses that CLI to push I/O to the boundary |
| **fsharp-pure-decorations** | VS Code extension: end-of-line **pure** / **impure** badges after Ionide LineLens |

| | |
|---|---|
| **Product site** | [https://fspure.net](https://fspure.net) (updated only on official release) |
| **Doc previews** | [github.io/fspure/preview](https://e-st.github.io/fspure/preview/) |
| **Source** | Everything you edit lives under [`src/`](src/) |

## Layout

| Path | Role |
|------|------|
| `src/` | Hand-authored source: products, tests, samples, docs, **F# tools** |
| `src/Fspure.Tasks/` | **F# monorepo task runner** (docs, security, gates, e2e phase1/5) |
| `src/docs/human/` | Hand-authored prose for generated docs (this file leads the README) |
| `.generated/` | Generated outputs (gitignored) |
| `.github/` | CI |
| `plugins/fspure/` | Published agent skill / Claude marketplace plugin |
| `flake.nix` | Optional .NET SDK shell only |

## Quick start (maintainers)

```text
dotnet run --project src/Fspure.Tasks -- help
dotnet run --project src/Fspure.Tasks -- docs preview
dotnet run --project src/Fspure.Tasks -- docs sync-readme
dotnet run --project src/Fspure.Tasks -- security
dotnet run --project src/Fspure.Tasks -- ready-lib-gate
dotnet run --project src/DevcontainerGen
```

End-user install (analyzer + extension + settings) is generated **below** this human section, and on **[fspure.net](https://fspure.net)**.

The **fspure-reduce-impurity** agent skill (Copilot / Claude) is documented in the next section.

Maintainer docs:

- [src/docs/LANGUAGES.md](src/docs/LANGUAGES.md) — F# first  
- [src/docs/DOCS.md](src/docs/DOCS.md) — docs / human anchors  
- [src/docs/RELEASING.md](src/docs/RELEASING.md) — release flow  
- [src/docs/AGENT.md](src/docs/AGENT.md) — `fspure analyze` for agents and CI  
