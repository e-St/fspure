# fspure agent CLI

`fspure analyze` is the contract for editors, CI, and coding agents. No IDE is required.

The same pure-set composition as the live analyser applies: **overrides > library embeds > foundational**.

The document is **facts only**: which impure function was called, inside which function, at which range. It does not say what to move. The agent decides that.

## Install

```bash
dotnet tool install -g fspure
```

Standalone linux-x64 (fstarter `bundlef` / PublishSingleFile):

```bash
cd src/fspure
bundlef -p
```

The binary is also staged inside `FSharp.PureAnalyzer` as `tools/fspure/` (next to `tools/fspure-collector/`).

From this monorepo:

```bash
dotnet run --project src/fspure -- analyze --project MyApp.fsproj --format json
dotnet run --project src/Fspure.Tasks -- analyze --project MyApp.fsproj --format json
```

## Command

```bash
fspure analyze \
  --project MyApp.fsproj \
  --focus src/Core \
  --ignore src/Host \
  --format json \
  --fail-on-impure \
  --cache-dir .fspure-cache
```

| Flag | Role |
|------|------|
| `--project` | `.fsproj` to typecheck (required unless `--sarif`) |
| `--focus` | Repeatable dir / file / glob. Restricts the report to the pure core. |
| `--ignore` | Repeatable. Applied after `--focus`. |
| `--format` | `json` (default) or `sarif` |
| `--fail-on-impure` | Exit **1** if any focused **impure call inside a function** remains |
| `--cache-dir` | Persist the filtered document (also `FSPURE_CACHE_DIR`) |
| `--analyzers-path` | Folder with `FSharp.PureAnalyzer.dll` |
| `--fsharp-analyzers` | Path to the `fsharp-analyzers` host |
| `--sarif` | Reuse an existing host SARIF (no re-typecheck) |
| `--output` | File path; default is stdout |
| `--verbose` | Progress on **stderr** only |

Exit codes: `0` clean, `1` focused impure-in-function calls with `--fail-on-impure`, `2` usage, `3` host failure.

JSON schema: [`src/fspure/fspure-analyze.schema.json`](../fspure/fspure-analyze.schema.json).

Each `impureCalls[]` row is:

- `caller` — enclosing function
- `callee` — the impure function that was called
- `file` / range — the **call site**
- `message` — analyser text (a fact)

The JSON (and SARIF) document is **byte-identical** for the same inputs: sorted calls, forward-slash relative paths, no timestamps.

## Agent loop

Defined for humans here; the executable skill at [`src/agent/fspure-push-impurity/SKILL.md`](../agent/fspure-push-impurity/SKILL.md) is what agents should load.

```text
edit → compile → fspure analyze --fail-on-impure --focus <core> --format json
     → if exit 1, read impureCalls (facts)
     → the agent decides which calls to move to the boundary
     → repeat until exit 0, or remaining calls are <10% and none is reasonably movable
```

## Wrappers

- GitHub Action: [`.github/actions/fspure-analyze/`](../../.github/actions/fspure-analyze/)
- Pre-commit: [`src/scripts/pre-commit-fspure-analyze.sh`](../scripts/pre-commit-fspure-analyze.sh)
