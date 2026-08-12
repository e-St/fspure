# fspure plugin

Skill: `fspure-reduce-impurity`.

**GitHub Copilot** — user install (not a repo folder):

```text
gh skill install e-St/fspure fspure-reduce-impurity \
  --scope user \
  --pin fspure-reduce-impurity-vX.Y.Z \
  --force \
  --agent github-copilot
```

`--agent` is required when there is no TTY (Codespaces / `postCreate`). `--pin` is the official skill tag (`fspure-reduce-impurity-v*`) from the Release PR. Until the first official skill tag exists, the Codespace pin is `main` (`v0.4.0` has no skill). The pin is `FSPURE_SKILL_REF` in the fstarter pack. The fspure and fstarter devcontainers run that command on create/attach, and put `fspure` on `PATH`.

**Claude Code:**

```text
/plugin marketplace add e-St/fspure
/plugin install fspure@fspure
```

CLI contract: `fspure analyze --fail-on-impure`. See `src/docs/AGENT.md`.
