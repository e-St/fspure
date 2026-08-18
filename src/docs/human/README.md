# Human Markdown partials

Hand-authored prose for generated docs. **Edit these files** — never the generated output under `.generated/`.

## How it works

1. Put a file at `src/docs/human/<id>.md` (this folder).
2. In a Scriban template, call:

   ```scriban
   {{ human "readme-top" }}
   ```

3. DocsGenerator inlines the file and wraps it:

   ```html
   <!-- <human id="readme-top"> -->
   …your Markdown…
   <!-- </human> -->
   ```

## Rules

- **Human content always comes from this directory** (source of truth).
- Put `{{ human "…" }}` where the prose belongs (README prologue is first; skill usage follows the 60-second install).
- Generated banners / install pins / snippets stay in the Scriban templates.
- Missing `{{ human "id" }}` fails generation (use `human_opt` only if optional).

To preview the generated site and Markdown **without pushing**:

```text
dotnet run --project src/DocsGenerator -- serve
```

Then open http://127.0.0.1:5500/ (Simple Browser) and `.generated/docs/` (Markdown preview). See [DOCS.md](../DOCS.md).

## Current partials

| File | Used by |
|------|---------|
| `readme-top.md` | `templates/README.md.scriban` — title, intro, screenshot |
| `skill-usage.md` | `templates/README.md.scriban`, `templates/customer.md.scriban` — how to install and use the agent skill |

Maintainer layout, repo map, and task commands live in [CONTRIBUTING.md](../CONTRIBUTING.md), not in the product README.
