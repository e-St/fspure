# Dev containers in this repo

There are **two** containers with different jobs. Do not mix them.

| Container | Path | Who uses it | Purpose |
|-----------|------|-------------|---------|
| **fspure IDE** (default) | [`.devcontainer/`](./devcontainer.json) | Codespaces, “Reopen in Container”, day-to-day work | Interactive VS Code: Ionide + PureAnalyzer + pure/impure decorations |
| **e2e (visual)** | [`e2e/phase2/.devcontainer/`](../e2e/phase2/.devcontainer/devcontainer.json) | GitHub Actions `e2e-customer.yml`, local phase 1/2 scripts | Headless CI image: code-server + Playwright + analyzer CLI |

## fspure IDE (this folder)

- **Image:** `ghcr.io/e-st/fstarter:latest` (F# / .NET / Paket / Ionide tooling).
- **On create/attach:** `setup-fspure-ide.sh` installs:
  1. `FSharp.PureAnalyzer` → workspace `analyzers/dotnet/fs/` (what Ionide loads)
  2. `fsharp-pure-decorations` VSIX (local package, Open VSX fallback)
- **Skipped in GitHub Actions** when `GITHUB_ACTIONS=true` (forwarded from the host via `remoteEnv` / `${localEnv:GITHUB_ACTIONS}`) so pack/build jobs are not slowed by IDE install. Extension install also soft-skips if the `code` CLI is not usable (typical for bare `postCreate` before attach).
- **Not** used by customer e2e CI.

Refresh after changing the analyzer or extension:

```bash
bash FSharp.PureAnalyzer/update-analyzer.sh
bash vscode-extension/update-extension.sh   # optional
# Developer: Reload Window
```

## e2e container

See [e2e/README.md](../e2e/README.md). Both e2e phases pin:

```text
e2e/phase2/.devcontainer/devcontainer.json
```

That definition is independent of this IDE container so Codespaces/setup changes cannot break visual e2e.
