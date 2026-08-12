# Sync fspure integration pack → `e-St/fstarter` (pull request)

The **source of truth** for how fstarter enables fspure is:

```text
e-St/fspure  →  src/scripts/integrations/fstarter/
```

Target:

```text
https://github.com/e-St/fstarter
```

Unlike `fspure-ready-lib` (force-push satellite), **fstarter** receives a **pull request** so template maintainers can review pin and setup changes. The title names the actual change (`fspure: pin skill main → fspure-reduce-impurity-v0.1.1`, `fspure: update Codespace setup`, …). The body leads with what Codespaces will do after merge, lists only pins that change, and names only pack files that differ. A run that would only bump `.fspure-sync-source` does **not** open a PR. Skill-only edits on fspure do not open an fstarter PR (the skill is not copied into the template; the pin is).

```mermaid
flowchart LR
  A[Edit src/scripts/integrations/fstarter or publish release] --> B[PR fspure updates to fstarter]
  B --> C[Branch on fstarter]
  C --> D[Open or update PR]
  D --> E[Human merges PR]
```

## What gets updated

| Path in fstarter | Source |
|------------------|--------|
| `.devcontainer/setup-fspure.sh` | `src/scripts/integrations/fstarter/overlay/.devcontainer/setup-fspure.sh` |
| `.devcontainer/devcontainer.json` | overlay (fspure Ionide / decorations settings) |
| `.devcontainer/fspure-versions.env` | generated pins (`FSPURE_ANALYZER_VERSION`, `FSPURE_SKILL_REF`) |
| `Directory.Build.props` | strict compiler rules (FS0025, TreatWarningsAsErrors, Nullable) |
| `.fspure-sync-source` | sync metadata |
| `.gitignore` | ensures `**/analyzers/` is ignored |

**Not overwritten:** `Dockerfile`, `newf.sh`, `bundlef.sh`, and other fstarter-owned scripts.

## Version pin

`FSPURE_ANALYZER_VERSION`, `FSPURE_SKILL_REF`, and `FSPURE_CLI_RELEASE` are written to `.devcontainer/fspure-versions.env` and read by `setup-fspure.sh`. The analyzer pin installs that exact package from nuget.org. The skill ref is the official tag `fspure-reduce-impurity-v{version}` after a skill publish (otherwise `main` until that first tag exists). It is passed to `gh skill install --pin` with `--agent github-copilot` (no TTY). The CLI release is the standalone `fspure` binary installed to `~/.local/bin`. Do not omit `--agent`: `gh` then prompts and Codespaces cancel. A fork of fstarter keeps that pin until it merges an fstarter update.

Priority when resolving the pin:

1. Workflow input `analyzer_version` (manual dispatch)
2. GitHub Release tag (e.g. `v0.4.0` → `0.4.0`)
3. `src/scripts/integrations/fstarter/versions.env` in fspure
4. Latest stable version on nuget.org

## One-time setup

`GITHUB_TOKEN` from fspure **cannot** open PRs on another repository.

### Fine-grained PAT

1. Create a fine-grained PAT with access **only** to **`e-St/fstarter`**.
2. Repository permissions:

   | Permission | Access |
   |------------|--------|
   | **Contents** | Read and write |
   | **Pull requests** | Read and write |

3. In **`e-St/fspure`** → **Settings → Secrets and variables → Actions**:
   - Name: **`FSPURE_FSTARTER_TOKEN`**
   - Value: the PAT

## When the workflow runs

| Trigger | Workflow |
|---------|----------|
| Push to `main` changing `src/scripts/integrations/fstarter/**` | **PR fspure updates to fstarter** |
| GitHub Release published | same (pins from release tag when possible) |
| Manual **Actions → PR fspure updates to fstarter** | same (`analyzer_version`, `dry_run`) |

Skill-only commits do **not** open an fstarter PR. Official skill publish updates `FSPURE_SKILL_REF` in `versions.env`, and that is what opens the pin PR.

## Day-to-day

1. Edit **`src/scripts/integrations/fstarter/`** in fspure (overlay src/scripts/settings, `versions.env`).
2. Merge to **main** (or publish a release / run the workflow manually with a version).
3. Workflow opens (or updates) a PR on **fstarter**.
4. Review and merge the PR on fstarter.

Local dry-run:

```bash
git clone https://github.com/e-St/fstarter.git /tmp/fstarter
bash src/scripts/prepare-fstarter-update.sh /tmp/fstarter 0.4.0
cd /tmp/fstarter && git status && git diff
```

## Relation to nuget.org publish

After **Publish Pure Analyzer to nuget.org** ships a new `FSharp.PureAnalyzer` version:

1. Bump `src/scripts/integrations/fstarter/versions.env` (or pass `analyzer_version` on dispatch).
2. Run **PR fspure updates to fstarter** (or push the versions.env change to main).

Optional: chain from release only — the release event already triggers this workflow.
