# Dev containers in this repo

There are **three** flavors. They share one config base via **fragments** (not Dev Container Features — those are not used here).

| Flavor | Generated file | Who uses it | Purpose |
|--------|----------------|-------------|---------|
| **IDE** (default) | [`.devcontainer/devcontainer.json`](./devcontainer.json) | Codespaces, “Reopen in Container” | Interactive VS Code + pure/impure labels |
| **build** | [`FSharp.PureAnalyzer/.devcontainer/devcontainer.json`](../FSharp.PureAnalyzer/.devcontainer/devcontainer.json) | Pack/build CI | Headless `dotnet` / `paket` / `bundlef` |
| **e2e** | [`e2e/phase2/.devcontainer/devcontainer.json`](../e2e/phase2/.devcontainer/devcontainer.json) | `e2e-customer.yml` | code-server + Playwright screenshots |

## Shared base (source of truth)

Dev Containers have **no** native `extends` for `devcontainer.json`. This repo keeps small JSON fragments and merges them:

```text
.devcontainer/fragments/
  flavours.json           # which fragments → which output path
  base.json               # shared: remoteUser, remoteEnv (DOTNET_*, TMPDIR)
  vscode-common.json      # shared: Ionide + pure-decoration settings
  flavours/
    ide.json              # IDE-only: image, postCreate/postAttach, fspure.slnx
    build.json            # build-only: name + image (no vscode lifecycle)
    e2e.json              # e2e-only: Dockerfile build, ports, fixture settings
```

**Edit fragments only.** Then regenerate the three committed outputs:

```bash
python3 .devcontainer/generate.py          # write
python3 .devcontainer/generate.py --check  # fail if stale
```

CI:

- [`.github/workflows/generate-devcontainers.yml`](../.github/workflows/generate-devcontainers.yml) — on fragment/generator changes, regenerate and **commit + push** (same-repo). Fork PRs must run generate locally if outputs are stale.
- [`.github/workflows/devcontainer-flavours.yml`](../.github/workflows/devcontainer-flavours.yml) — **reusable** job that every workflow using `devcontainers/ci` must `needs` first; callers then apply the artifact via [`.github/actions/apply-devcontainer-flavours`](../.github/actions/apply-devcontainer-flavours/action.yml).

Generated files start with `// GENERATED FILE` — do not hand-edit them.

### Image / Dockerfile layering

| Flavor | Runtime base |
|--------|----------------|
| IDE, build | `image`: `ghcr.io/e-st/fstarter:latest` |
| e2e | `Dockerfile` with `FROM ghcr.io/e-st/fstarter:latest`, then only code-server + Playwright deps + `fsharp-analyzers` |

So tooling (`.NET` / Paket / Node from **fstarter**) is maintained once; e2e only maintains its extra layers in `e2e/phase2/.devcontainer/Dockerfile`.

## fspure IDE (default)

- **On create/attach:** `setup-fspure-ide.sh` → analyzer DLL under `analyzers/` + decorations VSIX.
- Soft-skips extension install if `code` CLI is unusable; `postAttach` re-runs.
- Optional: `SKIP_FSPURE_IDE_SETUP=1`.

```bash
bash FSharp.PureAnalyzer/update-analyzer.sh
bash vscode-extension/update-extension.sh   # optional
# Developer: Reload Window
```

## PureAnalyzer build

```yaml
configFile: FSharp.PureAnalyzer/.devcontainer/devcontainer.json
```

```bash
devcontainer up --workspace-folder . --config FSharp.PureAnalyzer/.devcontainer/devcontainer.json
devcontainer exec --workspace-folder . --config FSharp.PureAnalyzer/.devcontainer/devcontainer.json \
  bash -lc 'cd FSharp.PureAnalyzer && paket restore && dotnet build -c Release'
```

## e2e

See [e2e/README.md](../e2e/README.md). Pins:

```text
e2e/phase2/.devcontainer/devcontainer.json
```

Needs pull access to `ghcr.io/e-st/fstarter` (GHCR login in CI).
