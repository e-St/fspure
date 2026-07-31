# fstarter ← fspure integration pack

Apply into [e-St/fstarter](https://github.com/e-St/fstarter) for customer.md **§0**.

## Apply

```bash
FSTARTER=../fstarter
PACK=integrations/fstarter/overlay
cp -a "$PACK/.devcontainer/." "$FSTARTER/.devcontainer/"
chmod +x "$FSTARTER/.devcontainer/setup-fspure.sh"
# merge analyzers/ into .gitignore if missing
```

## LineLens spacing (important)

Ionide renders signatures as `prefix + type`. Use:

```json
"FSharp.lineLens.prefix": "  // "
```

- **Leading spaces** — gap after `=`  
- **Trailing space after `//`** — so you get `// unit -> …`, **not** `//unit -> …`

Badges use two ASCII spaces before `pure` / `impure` so the line reads:

```text
let add a b =  // unit -> 'a -> unit  impure
```

## Layout

```text
integrations/fstarter/overlay/.devcontainer/
  devcontainer.json
  setup-fspure.sh
```
