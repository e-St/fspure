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

**Never delete an effect. Never hoist a deferred effect to top-level** (that would run it at module load). If a function body contains an impure call, make that function **higher-order**: take the impurity as a function argument (dependencies first). The core stays testable — tests pass `ignore` (or a fake); the host passes `printf` / real I/O. Do not add live call sites that were not already executing. Show host/test wiring only in comments.

Stay idiomatic F#: curried `let add hello x y`, not tupled, not an interface/`type`/class just to inject I/O. Pass `printf "%s"` or `fun () -> printf "hello"` at the edge; tests use `ignore`. If the effect is on the result, the injected function can take that value (`// add (printfn "%d") 2 3`).

```
// before                         // after
let add x y =                     let add hello x y =
    printf "hello"                    hello ()
    x + y                             x + y
                                  // host (only if add was already called):
                                  // add (fun () -> printf "hello") 2 3
                                  // tests:
                                  // add ignore 2 3
```

Do not write `printf "hello"` at file scope. Do not drop, skip, or "simplify away" the effect.

Loop: `dotnet build` → analyze → inject each impurity as a function argument → repeat. Exit 0 and no leftover focused effects is done. Exit 2/3 is a tool error.

Stop when remaining focused calls are ≤ 10% of the first `summary.impureCalls` and none still belong in the core. Cap 25 iterations.

In the fspure repo only, if PATH has no `fspure`: `dotnet run --project src/fspure -- analyze …`.
