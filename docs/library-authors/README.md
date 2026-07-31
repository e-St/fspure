# Library authors (third-party purity)

How libraries participate in PureAnalyzer **without** hand-written purity attributes.

| Document | Purpose |
|----------|---------|
| [USER_GUIDE.md](USER_GUIDE.md) | What to generate, embed, and ship (`purity.json`) |
| [templates/](templates/) | GitHub workflow, MSBuild props, sample JSON |
| [../strategy/purity-json-extension.md](../strategy/purity-json-extension.md) | How PureAnalyzer will discover and merge pure sets |

**Runtime rule (target behavior):**

1. PureAnalyzer always loads its **foundational** purity set.  
2. For each **DLL referenced by the project**, if a **`purity.json`** is available (embedded resource and/or sidecar), those pure methods are **unioned** into the pure set used for checks.
