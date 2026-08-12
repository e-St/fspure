---
name: fspure-reduce-impurity
description: >
  Push side effects out of F# core logic with `fspure analyze`.
  Use when making F# purer, fixing impure-in-pure, or the user mentions
  fspure analyze, PURE001, or PURE002.
license: MIT
---

# fspure-reduce-impurity

```
fspure analyze --project <fsproj> --focus <core> --ignore <io> --format json --fail-on-impure
```

`impureCalls[]` is the only truth: `caller`, `callee`, call-site range. Facts, not a move list. You decide.

Loop: compile → analyze → on exit 1, decide whether each call belongs in `caller` or at the I/O boundary → edit → repeat. Exit 0 is done. Exit 2/3 is a tool error.

Stop when remaining focused calls are ≤ 10% of the first `summary.impureCalls` and none still belong in the core. Cap 25 iterations.

Prefer `fspure` on PATH. In the fspure repo: `dotnet run --project src/fspure -- analyze …`.
