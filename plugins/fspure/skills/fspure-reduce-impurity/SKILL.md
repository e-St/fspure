---
name: fspure-reduce-impurity
description: >
  Push side effects out of F# core logic with `fspure analyze`.
  Use when making F# purer, fixing impure-in-pure, or the user mentions
  fspure analyze, PURE001, or PURE002.
license: MIT
---

# fspure-reduce-impurity

`fspure` is already on PATH in fspure / fstarter Codespaces. Do **not** download GitHub releases, search nuget, clone trees, or inspect `~/.nuget`. If `command -v fspure` fails, stop and tell the user to rebuild the container.

```
fspure analyze --project <fsproj> --focus <core> --ignore <io> --format json --fail-on-impure
```

`impureCalls[]` is the call-site report: `caller`, `callee`, range. Facts, not a move list. If it is empty but a focused function is still impure (PURE002, or I/O you can see in a `let`), treat those effects as work too.

**Never delete an effect.** Relocate it to the application / I/O boundary (`main`, top-level, host). The program must still do the same visible work (`printf "hello"` stays a `printf "hello"`, just not inside `add`). Do not drop, skip, or "simplify away" side effects to make a function look pure.

Loop: `dotnet build` → analyze → move each effect to the boundary → repeat. Exit 0 and no leftover focused effects is done. Exit 2/3 is a tool error.

Stop when remaining focused calls are ≤ 10% of the first `summary.impureCalls` and none still belong in the core. Cap 25 iterations.

In the fspure repo only, if PATH has no `fspure`: `dotnet run --project src/fspure -- analyze …`.
