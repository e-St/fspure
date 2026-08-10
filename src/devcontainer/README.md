# Devcontainer fragments

**Source of truth:** `src/devcontainer/fragments/`  
**Generator:** `dotnet run --project src/DevcontainerGen`  
**Outputs:** `.generated/devcontainer/{ide,build,e2e}/devcontainer.json` (gitignored)

Platform materializations (also written by the generator):

| Flavour | Platform path | Committed? |
|---------|---------------|------------|
| `ide` | `.devcontainer/devcontainer.json` | **yes** (Codespaces entry) |
| `build` | `src/FSharp.PureAnalyzer/.devcontainer/devcontainer.json` | no (CI generates) |
| `e2e` | `src/tests/e2e/phase2/.devcontainer/devcontainer.json` | no (CI generates) |

## Regenerate

```bash
dotnet run --project src/DevcontainerGen          # write
dotnet run --project src/DevcontainerGen --check  # fail if .generated copies stale
```

## CI

- [`.github/workflows/generate-devcontainers.yml`](../../.github/workflows/generate-devcontainers.yml) — on fragment/generator changes, regenerate and **commit the root Codespaces entry** only.
- [`.github/workflows/devcontainer-flavours.yml`](../../.github/workflows/devcontainer-flavours.yml) — reusable job that generates all flavours for CI consumers (artifact).

Edit **fragments**, not generated files.
