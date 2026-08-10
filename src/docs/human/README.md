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
- Put `{{ human "…" }}` **above** generated sections when humans must lead (e.g. README top + Layout + Quick start).
- Generated banners / install pins / snippets go **below** human anchors.
- Missing `{{ human "id" }}` fails generation (use `human_opt` only if optional).

## Current partials

| File | Used by |
|------|---------|
| `readme-top.md` | `templates/README.md.scriban` — title, links table, layout, quick start |
