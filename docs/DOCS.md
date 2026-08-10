# Documentation generation (F# + Scriban)

## Publishing policy (important)

| Surface | When it updates | URL |
|---------|-----------------|-----|
| **fspure.net** | **Only** when **Official release** finishes successfully | https://fspure.net/ |
| **GitHub Pages (github.io)** previews | Branch pushes (not `main`), beta/RC tags, manual “Docs preview” | https://e-st.github.io/fspure/preview/&lt;ref&gt;/ |
| **GitHub `main` README / docs/*.md** | Official release (and optional “Docs stable (Markdown only)”) | https://github.com/e-St/fspure |

Everyday commits and PRs **must not** change fspure.net.

## One-time GitHub setup

1. **Settings → Pages**
   - **Source:** Deploy from a branch  
   - **Branch:** `gh-pages`  
   - **Folder:** `/ (root)`  
   - **Custom domain:** `fspure.net` (HTTPS on)

2. **Do not** use “Deploy from branch `main` /docs” for the product site.  
   That would republish the custom domain on every docs change on `main`.

3. DNS for `fspure.net` stays pointed at GitHub Pages (unchanged if already working).

After the first **Official release** (or a one-time manual publish of `_site` with `cname: fspure.net`), the apex domain is the stable site only.

## Channels

### Preview (github.io only)

```bash
bash scripts/docs-generate.sh preview          # local → _site/preview/<branch>/
# CI: Docs preview → gh-pages under /preview/<ref>/
```

Open:

```text
https://e-st.github.io/fspure/preview/<sanitized-ref>/
```

Examples:

- branch `feature/foo` → `…/preview/feature-foo/`
- tag `v0.5.0-beta.1` → `…/preview/v0.5.0-beta.1/`
- PR `#12` → artifact only (no Pages publish from forks/PRs)

PRs always get a downloadable **Actions artifact**; non-PR pushes publish to github.io.

### Stable (fspure.net + main Markdown)

Runs inside **Official release** after packages publish:

1. Generate Markdown → commit to `main` (GitHub README)  
2. Publish `_site/` to **gh-pages root** with **`cname: fspure.net`**

Manual Markdown-only regen (does **not** touch fspure.net): workflow **Docs stable (Markdown only)**.

## Layout

| Path | Role |
|------|------|
| `docs/templates/*.scriban` | Edit these (source of truth) |
| `src/DocsGenerator/` | F# + Scriban generator |
| `scripts/docs-generate.sh` | CLI wrapper |
| `.github/workflows/docs-preview.yml` | github.io previews |
| `.github/workflows/official-release.yml` | **only** job that updates fspure.net |
| `.github/workflows/docs-stable.yml` | Markdown commit only (no domain publish) |

## Snippet markers

```fsharp
// <docs-snippet id="my-sample">
let add a b = a + b
// </docs-snippet>
```

```scriban
{{ snip "my-sample" }}
```

## ELI20 voice

Stable README: short install path, no fluff. Template: `docs/templates/README.md.scriban`.
