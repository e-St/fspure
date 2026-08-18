# Documentation generation (F# + Scriban)

## Publishing policy (important)

| Surface | When it updates | URL |
|---------|-----------------|-----|
| **fspure.net** | **Only** when **Official release** finishes successfully | https://fspure.net/ |
| **GitHub Pages (github.io)** previews | Pushes to any branch (including `main`), beta/RC tags, manual “Docs preview” | https://e-st.github.io/fspure/preview/&lt;ref&gt;/ |
| **GitHub `main` README** | Regenerated from `src/docs/human/` + templates on official release and when those files change | https://github.com/e-St/fspure |

Everyday commits and PRs **must not** change fspure.net.

Generated Markdown and the static site are written under **`.generated/`** (gitignored). The committed exceptions are the GitHub landing **`README.md`** and **`src/docs/EXAMPLES.md`**, which `sync-readme` writes from human files + templates and CI commits.

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

### Local (VS Code — no push)

Generate the same site and markdown that official release would publish, then preview them in the editor:

```text
dotnet run --project src/DocsGenerator -- serve
# or:  dotnet run --project src/Fspure.Tasks -- docs serve
# or:  VS Code → Terminal → Run Task… → docs: serve local fspure.net
```

| What | Where |
|------|--------|
| **fspure.net (local)** | http://127.0.0.1:5500/ — Command Palette → **Simple Browser: Show** and paste that URL, or Live Preview on `.generated/site/index.html` |
| **Generated Markdown** | `.generated/docs/README.md`, `customer.md`, `ARCHITECTURE.md`, `EXAMPLES.md` — open and **Markdown: Open Preview** |
| **Watch** | Edits under `src/docs/` regenerate both automatically |

This does **not** write the GitHub landing `README.md` or `src/docs/EXAMPLES.md` (use `sync-readme` when you want that) and does **not** publish fspure.net.

```text
dotnet run --project src/DocsGenerator -- stable          # one-shot generate
dotnet run --project src/DocsGenerator -- serve --port 5500
```

### Preview (github.io only)

```text
nix run .#docs -- preview                          # or: fspure-docs preview
dotnet run --project src/Fspure.Tasks -- docs preview  # CI / no Nix
dotnet run --project src/Fspure.Tasks -- docs sync-readme  # write root README.md + src/docs/EXAMPLES.md
# → .generated/site/preview/<branch>/
# CI: Docs preview → gh-pages under /preview/<ref>/
# CI: Sync docs markdown → commit README.md + src/docs/EXAMPLES.md from human files
```

Open:

```text
https://e-st.github.io/fspure/preview/<sanitized-ref>/
```

### Stable (fspure.net)

Runs inside **Official release** after packages publish:

1. `sync-readme`: generate Markdown + site → `.generated/`, write root `README.md` + `src/docs/EXAMPLES.md`  
2. Commit those files if they changed  
3. Publish `.generated/site/` to **gh-pages root** with **`cname: fspure.net`**

Manual generate-only (does **not** touch fspure.net): workflow **Docs stable (generate only)**.

## Layout

| Path | Role |
|------|------|
| `src/docs/human/<id>.md` | **Hand-authored** prose — always wins; never generated |
| `src/docs/templates/*.scriban` | Generated structure; call `{{ human "id" }}` for human blocks |
| `src/docs/templates/site/` | Site HTML/CSS templates only |
| `src/docs/**` (hand Markdown, assets, releases) | Edit these — **not** generated site pages |
| `src/DocsGenerator/` | F# + Scriban generator (**preview / stable / sync-readme**) |
| `src/Fspure.Tasks` | Monorepo CLI: `docs`, `security`, gates |
| `.generated/docs/` | Generated Markdown (gitignored) |
| `.generated/site/` | Generated static site (gitignored) |

## Human anchors

The product README is: human prologue (intro + screenshot + why/how) → generated **How can I use it?** (**Traditional Setup** + human **Agentic Setup**) → remaining generated sections.

1. Edit `src/docs/human/readme-top.md` (or add `src/docs/human/<id>.md`).
2. In the Scriban template, keep usage after the human prologue:

```scriban
{{ human "readme-top" }}

## How can I use it?
### Traditional Setup
…

### Agentic Setup
{{ human "skill-usage" }}
```

3. Output looks like:

```markdown
<!-- <human id="readme-top"> -->
…logo, screenshot, why/how…
<!-- </human> -->

## How can I use it?
…

### Traditional Setup
…

<!-- <human id="skill-usage"> -->
…skill install and rewrite…
```

- `{{ human "id" }}` — required; fails if the file is missing.  
- `{{ human_opt "id" }}` — optional empty.  
- Source of truth is always `src/docs/human/`, not the generated file.

Root `README.md` on GitHub is the **generated product README** (prologue + why/how + How can I use it? + what you get). Examples live in [EXAMPLES.md](EXAMPLES.md). Maintainer layout lives in [CONTRIBUTING.md](CONTRIBUTING.md). They are written by:

```text
dotnet run --project src/DocsGenerator -- sync-readme
dotnet run --project src/DocsGenerator -- sync-readme --check   # CI: fail if stale
```

| When | What happens |
|------|----------------|
| **Official release** | `sync-readme` then commit `README.md` + `src/docs/EXAMPLES.md` + publish fspure.net |
| **Push / PR** that touches `src/docs/human/`, templates, or the generator | Workflow **Sync docs markdown** regenerates and commits those files |
| **Docs generator check** | Renders preview artifacts (no commit) and asserts the skill section is present |

Do not hand-edit generated `README.md` or `src/docs/EXAMPLES.md`. Edit `src/docs/human/<id>.md` or the Scriban templates, then let the workflow (or `sync-readme`) rewrite them.

`.generated/` stays gitignored (site HTML, extra markdown). Committed generated Markdown is `README.md` and `src/docs/EXAMPLES.md`.

## Snippet markers (code from the repo)

```fsharp
// <docs-snippet id="my-sample">
let add a b = a + b
// </docs-snippet>
```

```scriban
{{ snip "my-sample" }}
```
