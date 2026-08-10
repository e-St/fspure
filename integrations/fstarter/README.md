# fstarter ← fspure integration pack

Source of truth for [e-St/fstarter](https://github.com/e-St/fstarter) fspure enablement.

## Automated PR (preferred)

Workflow: **PR fspure updates to fstarter** (`.github/workflows/pr-fstarter.yml`)

- Applies this pack into a branch on **fstarter**
- Opens a **pull request** (does not force-push `main`)
- Pins **FSharp.PureAnalyzer** via `.devcontainer/fspure-versions.env`

Setup and ops: [docs/SYNC-FSTARTER.md](../../docs/SYNC-FSTARTER.md)  
Secret on fspure: **`FSPURE_FSTARTER_TOKEN`**

## Manual apply

```bash
# From fspure monorepo root
git clone https://github.com/e-St/fstarter.git /tmp/fstarter
bash scripts/prepare-fstarter-update.sh /tmp/fstarter 0.4.0
```

Or copy overlay only:

```bash
FSTARTER=../fstarter
PACK=integrations/fstarter/overlay
cp -a "$PACK/.devcontainer/setup-fspure.sh" "$FSTARTER/.devcontainer/"
cp -a "$PACK/.devcontainer/devcontainer.json" "$FSTARTER/.devcontainer/"
chmod +x "$FSTARTER/.devcontainer/setup-fspure.sh"
# plus fspure-versions.env — see prepare-fstarter-update.sh
```

## LineLens spacing (important)

Ionide renders signatures as `prefix + type`. Use:

```json
"FSharp.lineLens.prefix": "  // "
```

- **Leading spaces** — gap after `=`  
- **Trailing space after `//`** — so you get `// unit -> …`, **not** `//unit -> …`

Badges use two ASCII spaces before `pure` / `impure`:

```text
let add a b =  // unit -> 'a -> unit  impure
```

## Layout

```text
integrations/fstarter/
  versions.env                 # FSPURE_ANALYZER_VERSION pin
  optional-newf.md             # optional newf.sh notes
  overlay/.devcontainer/
    devcontainer.json          # fspure-enabled Codespace settings
    setup-fspure.sh            # install analyzer + decorations
```

## Optional: newf package reference

See [optional-newf.md](./optional-newf.md) to also restore `FSharp.PureAnalyzer` on scaffolded projects (Ionide still uses the `analyzers/` drop from setup).
