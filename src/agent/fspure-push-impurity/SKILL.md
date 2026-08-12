---
name: fspure-push-impurity
description: >
  Push impurity to the application boundary in F# using fspure. Run
  `fspure analyze --fail-on-impure`, read the impureCalls facts (caller, callee,
  range), decide which of those calls can reasonably leave the pure core,
  refactor, compile, and repeat until at least 90% of focused impure-in-function
  calls are gone. Use when the user asks to make F# code purer, push side
  effects out, fix impure-in-pure, run fspure analyze, or prepare a PR/CI ticket
  for purity. Triggers: /fspure-push-impurity, "push impurity", "make this pure",
  "fspure analyze", "PURE001", "PURE002".
---

# Push impurity to the boundary

CLI contract, flags, schema, exit codes: `src/docs/AGENT.md`.
JSON shape: `src/fspure/fspure-analyze.schema.json`.

The CLI document is **facts**. Do not treat it as a to-do list. You decide what to move.

Do not invent call sites. Only `impureCalls[]` is truth.

## Setup

1. Prefer `fspure` on PATH. Else `dotnet run --project src/fspure -- analyze …` from the fspure repo, or `dotnet exec` the `tools/fspure/fspure.dll` next to `FSharp.PureAnalyzer`.
2. Agree `--focus` (pure core) and `--ignore` (I/O, controllers, `Program.fs`, adapters). If the user names a core folder, use it. Otherwise pick `src` minus obvious hosts.
3. Record the first JSON `summary.impureCalls` as `N0`.

## Loop (do not stop early)

```text
compile → fspure analyze --project <fsproj> --focus <core> --ignore <boundary> --format json --fail-on-impure --cache-dir .fspure-cache
```

- **exit 0** — done.
- **exit 2 / 3** — fix tool/project errors, do not treat as purity success.
- **exit 1** — read `impureCalls`. Each row is: `caller` (enclosing function), `callee` (impure function that was invoked), `file` + range (the **call site**).

For every remaining row, decide whether that **call** should leave `caller`:

| Often move the call out of `caller` | Often leave it |
|------|--------|
| `printf` / logging / metrics inside a calculation | The I/O adapter, logger implementation, HTTP handler, `main` |
| `File.*`, `HttpClient`, DB inside a compute function | Persistence/repository types whose job is I/O |
| `DateTime.Now`, `Random`, `Guid.NewGuid` inside logic | Clock/RNG injected at the edge |
| A helper that only exists to hide the effect | Local `let mutable` used as a loop accumulator |

Refactor by **moving the effect out** of `caller` and passing values in. Preserve behaviour. Do not add purity attributes. Do not weaken tests to hide the fact.

Then compile. If compile fails, fix that first. Then run `fspure analyze` again.

## 90% stop

Stop only when **all** of these hold:

1. No remaining focused row is one you judged as “often move”.
2. `summary.impureCalls <= max(0, ceil(0.10 * N0))`.
3. The project compiles.

If a remaining call is reasonably movable, **keep going**. Cap at 25 iterations; then report the leftover **facts** and your decision.

Stdout of `fspure analyze` is only the document. Use `--verbose` if you need host logs (stderr).
