# Releases

Official version numbers and changelogs for publishable components live here.

| Component | NuGet / marketplace id | Changelog |
|-----------|------------------------|-----------|
| Analyzer | `FSharp.PureAnalyzer` | [CHANGELOG.FSharp.PureAnalyzer.md](./CHANGELOG.FSharp.PureAnalyzer.md) |
| Collector tool | `fspure-collector` | [CHANGELOG.fspure-collector.md](./CHANGELOG.fspure-collector.md) |
| VS Code extension | `e-st.fsharp-pure-decorations` | [CHANGELOG.fsharp-pure-decorations.md](./CHANGELOG.fsharp-pure-decorations.md) |
| Agent skill | `fspure-reduce-impurity` | [CHANGELOG.fspure-reduce-impurity.md](./CHANGELOG.fspure-reduce-impurity.md) |

## Flow (short)

1. **Beta / CI** — any green main build can publish prereleases to GitHub Packages (`-ci.*` / `-beta.*`). No Release PR.
2. **Official** — run **Prepare release PR** → edit versions + changelogs on the PR → merge → **official publish** + pin updates + ready-lib / fstarter sync.

Full docs: [src/docs/RELEASING.md](../RELEASING.md).
