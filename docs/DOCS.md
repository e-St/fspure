# Documentation generation (F# + Scriban)

## Goals

1. **All public Markdown** (README, customer guide, architecture, …) is **generated**.
2. **Code samples** are cut from **real tests/source** via `<docs-snippet id="…">` markers — they cannot rot in the README.
3. **`main` Markdown is only rewritten on stable releases** (not on every PR).
4. **Every branch / beta** gets a **preview site** under `https://fspure.net/preview/<ref>/`.

## Layout

| Path | Role |
|------|------|
| `docs/templates/*.scriban` | Scriban sources (edit these) |
| `src/DocsGenerator/` | F# tool (`fspure-docs`) |
| `scripts/docs-generate.sh` | Thin CLI wrapper |
| `.github/workflows/docs-preview.yml` | PR/branch/beta → gh-pages preview |
| `.github/workflows/docs-stable.yml` | Stable → commit Markdown + site root |

Generated outputs (do not hand-edit):

- `README.md`
- `docs/customer.md`
- `docs/ARCHITECTURE.md`
- Static site files under `_site/` (gitignored) → published to **gh-pages**

## Snippet markers

In F# / any `//` file:

```fsharp
// <docs-snippet id="my-sample">
let add a b = a + b
// </docs-snippet>
```

In XML / fsproj:

```xml
<!-- <docs-snippet id="package-ref"> -->
<PackageReference Include="FSharp.PureAnalyzer" … />
<!-- </docs-snippet> -->
```

Templates call:

```
{{ snip "my-sample" }}
```

## Local commands

```bash
# Branch / PR preview (does not touch committed Markdown)
bash scripts/docs-generate.sh preview

# Stable (release only) — rewrites README.md + docs/*.md
bash scripts/docs-generate.sh stable 0.4.0
```

## Domains

| URL | Content |
|-----|---------|
| [fspure.net](https://fspure.net) | Stable site (updated on official release) |
| `https://fspure.net/preview/<branch>/` | Per-branch / beta / PR previews |

DNS: point `fspure.net` at GitHub Pages for the **gh-pages** branch (or keep serving `docs/` from main for the landing page and publish previews to gh-pages — both are supported by the workflows; prefer **gh-pages** for multi-version paths).

Optional later: map `preview.fspure.net` → same Pages site with a host-based redirect to `/preview/`.

## Policy

| Event | Generate? | Commit Markdown to `main`? | Publish site |
|-------|-----------|----------------------------|--------------|
| PR / feature branch | yes (preview) | **no** | `preview/<ref>/` |
| Beta / RC tag | yes (preview) | **no** | `preview/<tag>/` |
| Official release | yes (stable) | **yes** | site root + `main` files |

## ELI20 voice

Stable README style: short sentences, “npm install” energy, no marketing fluff. Templates live under `docs/templates/README.md.scriban`.
