---
name: fspure-push-impurity
description: >
  Push impurity to the application boundary in F# using fspure. Run
  `fspure analyze --fail-on-impure`, read impureCalls facts (caller, callee,
  range), decide which of those calls can reasonably leave the pure core,
  refactor, compile, and repeat until at least 90% of focused impure-in-function
  calls are gone. Use when the user asks to make F# code purer, push side
  effects out, run fspure analyze, or prepare a PR/CI ticket for purity.
  Triggers: /fspure-push-impurity, "push impurity", "make this pure",
  "fspure analyze", "PURE001", "PURE002".
---

Read and follow `src/agent/fspure-push-impurity/SKILL.md` from the repository root. That file is the only copy of the loop and decision table.
