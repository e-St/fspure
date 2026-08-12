---
name: fspure-reduce-impurity
description: >
  Push side effects out of F# core logic with `fspure analyze`.
  For F# aficionados and anyone else.
  Use when making F# purer, fixing impure-in-pure, or the user mentions
  fspure analyze, PURE001, or PURE002.
license: MIT
---

# fspure-reduce-impurity

For F# aficionados and anyone else.

`fspure` is already on PATH in fspure / fstarter Codespaces. Do **not** download GitHub releases, search nuget, clone trees, or inspect `~/.nuget`. If `command -v fspure` fails, stop and tell the user to rebuild the container.

```
fspure analyze --project <fsproj> --focus <core> --ignore <io> --format json --fail-on-impure
```

`impureCalls[]` is the call-site report: `caller`, `callee`, range. Facts, not a move list. Rewrite **every** row the same way — every focused function, every `callee`. Nothing here is limited to printing or a name list. If the report is empty but a focused function is still impure (PURE002, or an effect you can see in a `let`), treat those the same way.

**Never delete an effect. Never hoist a deferred effect to file scope** (that would run it at module load). For each impure call inside a function: keep that function's name and make it **higher-order**. Take the effect as a function argument (dependencies first; one parameter per distinct effect). Name the parameter for the role that effect plays in this function — what the caller needs done, not which library function implements it, and not a fixed vocabulary. Define the original call as a real example function named for what that call actually does. Those two names must differ.

Stay idiomatic F#: curried, not tupled, not an interface/`type`/class just to inject an effect. Tests can pass `ignore`. Do not add live call sites that were not already executing.

```
// shape only — same rewrite for every callee, not only printf
// before                         // after
let add x y =                     let printfHello s = printf "%s" s
    printf "hello"                let add write x y =
    x + y                             write "hello"
                                      x + y
```

`add` / `write` / `printfHello` are this illustration's names, not a whitelist. Do not hoist the original impure call to file scope. Do not drop, skip, or "simplify away" the effect.

Loop: `dotnet build` → analyze → inject each impurity as a function argument → repeat. Exit 0 and no leftover focused effects is done. Exit 2/3 is a tool error.

Stop when remaining focused calls are ≤ 10% of the first `summary.impureCalls` and none still belong in the core. Cap 25 iterations.

In the fspure repo only, if PATH has no `fspure`: `dotnet run --project src/fspure -- analyze …`.
