# fspure agent CLI

`fspure analyze` is the contract for editors, CI, and coding agents. No IDE is required.

The same pure-set composition as the live analyser applies: **overrides > library embeds > foundational**.

The document is **facts only**: which impure function was called, inside which function, at which range. It does not say what to move. The agent decides that.

## Install

```bash
dotnet tool install -g fspure
```

GitHub Copilot (VS Code agent mode, Copilot CLI, coding agent). Non-interactive install (GitHub CLI 2.90+):

```bash
gh skill install e-St/fspure fspure-reduce-impurity \
  --scope user \
  --pin main \
  --force \
  --agent github-copilot
```

`--agent github-copilot` is required without a TTY. `--pin main` is required until the next official tag: `gh skill` otherwise uses the latest GitHub Release (`v0.4.0`), which has no discoverable skill. The Codespace / devcontainer runs this command on create/attach, and installs the standalone `fspure` CLI to `~/.local/bin` (do not `gh release download` from the agent).

Claude Code:

```text
/plugin marketplace add e-St/fspure
/plugin install fspure@fspure
```

The skill body is `plugins/fspure/skills/fspure-reduce-impurity/SKILL.md`. Edits on `main` run **Update fspure plugin**, which bumps the marketplace plugin version.

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

Defined for humans here; the skill is [`plugins/fspure/skills/fspure-reduce-impurity/SKILL.md`](../plugins/fspure/skills/fspure-reduce-impurity/SKILL.md).

```text
edit → compile → fspure analyze --fail-on-impure --focus <core> --format json
     → if exit 1, read impureCalls (facts)
     → if the report is empty but a function is still impure, read the body
     → keep the function name; inject impurity as a generic-use-case argument (write, read, send, …)
     → define the outsourced effect as a real example function of that name (do not hoist, do not delete)
     → do not add executing calls that were not already running
     → repeat until exit 0, or remaining calls are <10% and none still belongs in the core
```

## Wrappers

- GitHub Action: [`.github/actions/fspure-analyze/`](../../.github/actions/fspure-analyze/)
- Pre-commit: [`src/scripts/pre-commit-fspure-analyze.sh`](../scripts/pre-commit-fspure-analyze.sh)
