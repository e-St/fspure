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

**Never delete an effect. Never hoist a deferred effect to top-level** (that would run it at module load). An effect that only lives in a function body is not executing yet — extract it into its own **impure function** so it still runs only when called. The original function becomes the pure remainder. Do not add live call sites that were not already executing. Show how to compose them only in comments.

Stay idiomatic F#: curried `let add x y`, not tupled `let add (x, y)`. Delay I/O with a `unit` argument (`let hello () = printf "hello"`), not `lazy` (memoizes) and not `do` / file-scope `printf`. If the effect consumes the value, compose at the edge with `|>` (`// add 2 3 |> printfn "%d"`). Independent effects stay a separate function — no class, interface, or computation expression just to split them.

```
// before                         // after
let add x y =                     let hello () = printf "hello"
    printf "hello"                let add x y = x + y
    x + y                         // later, if you used to call add and still want the effect:
                                  // hello ()
                                  // add 2 3
                                  // if the effect were on the result:
                                  // add 2 3 |> printfn "%d"
```

Do not write `printf "hello"` at file scope. Do not drop, skip, or "simplify away" the effect.

Loop: `dotnet build` → analyze → extract deferred effects into impure functions → repeat. Exit 0 and no leftover focused effects is done. Exit 2/3 is a tool error.

Stop when remaining focused calls are ≤ 10% of the first `summary.impureCalls` and none still belong in the core. Cap 25 iterations.

In the fspure repo only, if PATH has no `fspure`: `dotnet run --project src/fspure -- analyze …`.
