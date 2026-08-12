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
dotnet run --project src/Fspure.Tasks -- security
dotnet run --project src/Fspure.Tasks -- ready-lib-gate
dotnet run --project src/DevcontainerGen
```

End-user install (analyzer + extension + settings) is generated **below** this human section, and on **[fspure.net](https://fspure.net)**.

## Agent skill

**GitHub Copilot.** The Codespace / devcontainer installs the published skill into your user profile (not the repo):

```text
gh skill install e-St/fspure fspure-reduce-impurity --scope user
```

Needs GitHub CLI 2.90+. The fstarter / fspure Codespace installs `gh` if it is missing, then runs that command. After that, Copilot loads `fspure-reduce-impurity` when you talk about purity, `fspure analyze`, or PURE001.

**Claude Code.** This repo is a plugin marketplace. Add it and install the plugin:

```text
/plugin marketplace add e-St/fspure
/plugin install fspure@fspure
```

Then run `/fspure:fspure-reduce-impurity`, or let Claude pick the skill from the task. The skill lives in [`plugins/fspure/`](plugins/fspure/).

Maintainer docs:

- [src/docs/LANGUAGES.md](src/docs/LANGUAGES.md) — F# first  
- [src/docs/DOCS.md](src/docs/DOCS.md) — docs / human anchors  
- [src/docs/RELEASING.md](src/docs/RELEASING.md) — release flow  
- [src/docs/AGENT.md](src/docs/AGENT.md) — `fspure analyze` for agents and CI  
