# fspure plugin

Skill: `fspure-reduce-impurity`.

**GitHub Copilot** — user install (not a repo folder):

```text
gh skill install e-St/fspure fspure-reduce-impurity \
  --scope user \
  --pin main \
  --force \
  --agent github-copilot
```

`--agent` is required when there is no TTY (Codespaces / `postCreate`). `--pin main` is required until the next official GitHub Release tag includes `plugins/fspure/skills/` (`v0.4.0` does not). The fspure and fstarter devcontainers run that command on create/attach.

**Claude Code:**

```text
/plugin marketplace add e-St/fspure
/plugin install fspure@fspure
```

CLI contract: `fspure analyze --fail-on-impure`. See `src/docs/AGENT.md`.
