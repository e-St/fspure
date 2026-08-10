# Documentation generation (F# + Scriban)

## Publishing policy (important)

| Surface | When it updates | URL |
|---------|-----------------|-----|
| **fspure.net** | **Only** when **Official release** finishes successfully | https://fspure.net/ |
| **GitHub Pages (github.io)** previews | Branch pushes (not `main`), beta/RC tags, manual “Docs preview” | https://e-st.github.io/fspure/preview/&lt;ref&gt;/ |
| **GitHub `main` README** | Hand-authored short stub (not generated product docs) | https://github.com/e-St/fspure |

Everyday commits and PRs **must not** change fspure.net.

Generated Markdown and the static site are written only under **`.generated/`** (gitignored). They are never committed into `src/` or the repo root.

## One-time GitHub setup

1. **Settings → Pages**
   - **Source:** Deploy from a branch  
   - **Branch:** `gh-pages`  
   - **Folder:** `/ (root)`  
   - **Custom domain:** `fspure.net` (HTTPS on)

2. **Do not** use “Deploy from branch `main` /docs” for the product site.  
   That would republish the custom domain on every docs change on `main`.

3. DNS for `fspure.net` stays pointed at GitHub Pages (unchanged if already working).

After the first **Official release** (or a one-time manual publish of `.generated/site` with `cname: fspure.net`), the apex domain is the stable site only.

## Channels

### Preview (github.io only)

```text
nix run .#docs -- preview                          # or: fspure-docs preview
dotnet run --project src/Fspure.Tasks -- docs preview  # CI / no Nix
# → .generated/site/preview/<branch>/
# CI: Docs preview → gh-pages under /preview/<ref>/
```

Open:

```text
https://e-st.github.io/fspure/preview/<sanitized-ref>/
```

### Stable (fspure.net)

Runs inside **Official release** after packages publish:

1. Generate Markdown + site → `.generated/` (ephemeral, not committed)  
2. Publish `.generated/site/` to **gh-pages root** with **`cname: fspure.net`**

Manual generate-only (does **not** touch fspure.net): workflow **Docs stable (generate only)**.

## Layout

| Path | Role |
|------|------|
| `src/docs/human/<id>.md` | **Hand-authored** prose — always wins; never generated |
| `src/docs/templates/*.scriban` | Generated structure; call `{{ human "id" }}` for human blocks |
| `src/docs/templates/site/` | Site HTML/CSS templates only |
| `src/docs/**` (hand Markdown, assets, releases) | Edit these — **not** generated site pages |
| `src/DocsGenerator/` | F# + Scriban generator (**preview/stable** modes) |
| `src/Fspure.Tasks` | Monorepo CLI: `docs`, `security`, gates |
| `.generated/docs/` | Generated Markdown (gitignored) |
| `.generated/site/` | Generated static site (gitignored) |

## Human anchors

Human instructions must stay above generated install noise where it matters.

1. Edit `src/docs/human/readme-top.md` (or add `src/docs/human/<id>.md`).
2. In the Scriban template, put the human block **first**:

```scriban
{{ human "readme-top" }}

<!-- generated sections below -->
## 60-second install
…
```

3. Output looks like:

```markdown
<!-- <human id="readme-top"> -->
# fspure
…
<!-- </human> -->

## 60-second install
…
```

- `{{ human "id" }}` — required; fails if the file is missing.  
- `{{ human_opt "id" }}` — optional empty.  
- Source of truth is always `src/docs/human/`, not the generated file.

Root `README.md` on GitHub can mirror `human/readme-top.md` for maintainers; full product README is produced into `.generated/` / the site on generate.

## Snippet markers (code from the repo)

```fsharp
// <docs-snippet id="my-sample">
let add a b = a + b
// </docs-snippet>
```

```scriban
{{ snip "my-sample" }}
```
