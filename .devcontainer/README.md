# Dev containers in this repo

There are **three** containers with different jobs. Do not mix them.

| Container | Path | Who uses it | Purpose |
|-----------|------|-------------|---------|
| **fspure IDE** (default) | [`.devcontainer/devcontainer.json`](./devcontainer.json) | Codespaces, “Reopen in Container”, day-to-day work | Interactive VS Code: Ionide + PureAnalyzer + pure/impure decorations |
| **PureAnalyzer build** | [`FSharp.PureAnalyzer/.devcontainer/`](../FSharp.PureAnalyzer/.devcontainer/devcontainer.json) | GitHub Actions pack/build (`build-pure-analyzer`, `release-pure-analyzer`; also reused by `bundle-purity-collector`, `generate-list-a`) | Headless `dotnet` / `paket` / `bundlef` — no IDE install |
| **e2e (visual)** | [`e2e/phase2/.devcontainer/`](../e2e/phase2/.devcontainer/devcontainer.json) | GitHub Actions `e2e-customer.yml`, local phase 1/2 scripts | Headless CI image: code-server + Playwright + analyzer CLI |

## fspure IDE (default, this folder)

- **Image:** `ghcr.io/e-st/fstarter:latest` (F# / .NET / Paket / Ionide tooling).
- **On create/attach:** `setup-fspure-ide.sh` installs:
  1. `FSharp.PureAnalyzer` → workspace `analyzers/dotnet/fs/` (what Ionide loads)
  2. `fsharp-pure-decorations` VSIX (local package, Open VSX fallback)
- Extension install soft-skips if the `code` CLI is not usable (typical for bare `postCreate` before attach); `postAttach` re-runs setup.
- **Not** used by pack/build CI or customer e2e.

Refresh after changing the analyzer or extension:

```bash
bash FSharp.PureAnalyzer/update-analyzer.sh
bash vscode-extension/update-extension.sh   # optional
# Developer: Reload Window
```

Optional: `SKIP_FSPURE_IDE_SETUP=1` to skip install on create/attach.

## PureAnalyzer build (`FSharp.PureAnalyzer/.devcontainer/`)

- Same base image as the IDE (`fstarter`), but **no** `postCreate` / `postAttach`, no VS Code extensions, no pure-label setup.
- Lives next to the analyzer project so pack/build CI is scoped with the code it builds.
- Workflows must pass:

```yaml
configFile: FSharp.PureAnalyzer/.devcontainer/devcontainer.json
```

Local dry-run (from repo root):

```bash
devcontainer up --workspace-folder . --config FSharp.PureAnalyzer/.devcontainer/devcontainer.json
devcontainer exec --workspace-folder . --config FSharp.PureAnalyzer/.devcontainer/devcontainer.json \
  bash -lc 'cd FSharp.PureAnalyzer && paket restore && dotnet build -c Release'
```

## e2e container

See [e2e/README.md](../e2e/README.md). Both e2e phases pin:

```text
e2e/phase2/.devcontainer/devcontainer.json
```

That definition is independent of the IDE and PureAnalyzer build containers so setup changes cannot break visual e2e.
