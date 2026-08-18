# Contributing

Maintainer map for this repo. End-user install is in the root [README](../../README.md) and on [fspure.net](https://fspure.net).

## Layout

| Path | Role |
|------|------|
| `src/` | Hand-authored source: products, tests, samples, docs, **F# tools** |
| `src/Fspure.Tasks/` | **F# monorepo task runner** (docs, security, gates, e2e phase1/5) |
| `src/docs/human/` | Hand-authored prose for generated docs (README prologue + skill usage) |
| `.generated/` | Generated outputs (gitignored) |
| `.github/` | CI |
| `plugins/fspure/` | Published agent skill / Claude marketplace plugin |
| `flake.nix` | Optional .NET SDK shell only |

```text
src/              analyzer, schema, collector, embed, F# tools
src/Fspure.Tasks  monorepo CLI (docs, security, gates, phase1/5)
src/tests/        unit + e2e
src/samples/      fspure-ready-lib template
src/editor/       VS Code extension
src/docs/         hand docs, human/ partials, templates, releases
.generated/       docs site + markdown (gitignored)
```

## Quick start

```text
dotnet run --project src/Fspure.Tasks -- help
dotnet run --project src/Fspure.Tasks -- docs preview
dotnet run --project src/Fspure.Tasks -- docs sync-readme
dotnet run --project src/Fspure.Tasks -- security
dotnet run --project src/Fspure.Tasks -- ready-lib-gate
dotnet run --project src/DevcontainerGen
```

## Docs channels

| Channel | When | Where |
|---------|------|--------|
| **Stable** | **Official release only** | **[fspure.net](https://fspure.net)** (+ generated site under `.generated/`) |
| **Preview** | Any branch (including `main`) / beta tags | **[github.io](https://e-st.github.io/fspure/preview/)** only — not fspure.net |

Templates: `src/docs/templates/`. Human prose: `src/docs/human/`. Generator: `src/DocsGenerator`. Policy: [DOCS.md](DOCS.md).

```text
dotnet run --project src/Fspure.Tasks -- docs preview
dotnet run --project src/Fspure.Tasks -- docs stable
```

## Maintainer docs

- [EXAMPLES.md](EXAMPLES.md) — generated fixture / ready-lib snippets
- [LANGUAGES.md](LANGUAGES.md) — F# first
- [DOCS.md](DOCS.md) — docs / human anchors
- [RELEASING.md](RELEASING.md) — release flow
- [AGENT.md](AGENT.md) — `fspure analyze` for agents and CI
- [SECURITY.md](SECURITY.md) — security
