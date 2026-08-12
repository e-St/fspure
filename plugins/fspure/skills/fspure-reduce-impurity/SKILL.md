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

**Never delete an effect. Never hoist a deferred effect to top-level** (that would run it at module load). If a function body contains an impure call, keep the function's name and make it **higher-order**: take the impurity as a function argument (dependencies first). Name that parameter by use case, not the old callee — `write`, `read`, `send`, `log`, … Define the outsourced effect as a real example function with the same name.

Stay idiomatic F#: curried `let add write x y`, not tupled, not an interface/`type`/class just to inject I/O. Tests can pass `ignore`. Do not add live call sites that were not already executing.

```
// before                         // after
let add x y =                     let write s = printf "%s" s
    printf "hello"                let add write x y =
    x + y                             write "hello"
                                      x + y
```

Do not write `printf "hello"` at file scope. Do not drop, skip, or "simplify away" the effect.

Loop: `dotnet build` → analyze → inject each impurity as a function argument → repeat. Exit 0 and no leftover focused effects is done. Exit 2/3 is a tool error.

Stop when remaining focused calls are ≤ 10% of the first `summary.impureCalls` and none still belong in the core. Cap 25 iterations.

In the fspure repo only, if PATH has no `fspure`: `dotnet run --project src/fspure -- analyze …`.
