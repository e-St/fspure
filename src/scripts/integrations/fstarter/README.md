# fstarter ← fspure integration pack

Source of truth for [e-St/fstarter](https://github.com/e-St/fstarter) fspure enablement.

## Automated PR (preferred)

Workflow: **PR fspure updates to fstarter** (`.github/workflows/pr-fstarter.yml`)

- Applies this pack into a branch on **fstarter**
- Opens a **pull request** (does not force-push `main`)
- Pins **FSharp.PureAnalyzer** via `.devcontainer/fspure-versions.env`

Setup and ops: [src/docs/SYNC-FSTARTER.md](../../src/docs/SYNC-FSTARTER.md)  
Secret on fspure: **`FSPURE_FSTARTER_TOKEN`**

## Manual apply

```bash
# From fspure monorepo root
git clone https://github.com/e-St/fstarter.git /tmp/fstarter
bash src/scripts/prepare-fstarter-update.sh /tmp/fstarter 0.4.0
```

Or copy overlay only:

```bash
FSTARTER=../fstarter
PACK=src/scripts/integrations/fstarter/overlay
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
src/scripts/integrations/fstarter/
  versions.env                 # FSPURE_ANALYZER_VERSION pin
  optional-newf.md             # optional newf.sh notes
  assert-overlay-contract.sh   # refuse postAttach / features / skipped VSIX
  test-overlay-contract.sh
  overlay/
    Directory.Build.props      # same strict F# rules as fspure monorepo
    .devcontainer/
      devcontainer.json        # postCreate-only; baked analyzersPath; no features
      setup-fspure.sh          # baked-first analyzer + decorations VSIX unpack
```

Do **not** restore `postAttachCommand` or a `features` / `github-cli` block. `e-st.fsharp-pure-decorations` is Open VSX only — `setup-fspure.sh` unpacks the baked VSIX even when `code` is unusable. `prepare-fstarter-update.sh` refuses an overlay that would undo that.

## Optional: newf package reference

See [optional-newf.md](./optional-newf.md) to also restore `FSharp.PureAnalyzer` on scaffolded projects (Ionide still uses the `analyzers/` drop from setup).
