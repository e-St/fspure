The **fspure-reduce-impurity** skill teaches your coding agent to push side effects out of F# core logic. You describe what should stay pure; the agent runs `fspure analyze` and rewrites each impure call so the effect is passed in as a function argument.

The original I/O is not deleted. It moves to the boundary of the application.

#### Install the skill

**GitHub Copilot** (VS Code agent mode, Copilot CLI, or coding agent). Needs [GitHub CLI](https://cli.github.com/) 2.90+:

```text
gh skill install e-St/fspure fspure-reduce-impurity \
  --scope user \
  --pin main \
  --agent github-copilot
```

An [fspure](https://github.com/e-St/fspure) or [fstarter](https://github.com/e-St/fstarter) Codespace already does this on create. After the first official skill release you can pin a tag (`fspure-reduce-impurity-v*`) instead of `main`.

**Claude Code:**

```text
/plugin marketplace add e-St/fspure
/plugin install fspure@fspure
```

Then run `/fspure:fspure-reduce-impurity`, or just describe the task and let Claude pick the skill.

#### How to use it

1. Add the analyzer to the project so the agent can run `fspure analyze` (the Traditional Setup on this page, or [fspure.net/get-started](https://fspure.net/get-started.html)).
2. Point the agent at the code that should stay pure, for example:

   > Make `src/Core` purer. Ignore `src/Host`.

   You can also say “fix this PURE002” or “push I/O out of this function.”
3. The agent loops: build → `fspure analyze --fail-on-impure` → rewrite → repeat.
4. It is done when the report is clean, or when only a little impurity remains and it belongs at the edge of the app.

#### What a rewrite looks like

```fsharp
// before                         // after
let add x y =                     let printfHello s = printf "%s" s
    printf "hello"                let add write x y =
    x + y                             write "hello"
                                      x + y
```

The function keeps its name. Each effect becomes a parameter named for the **role** it plays (`write`, not `printf`). The original call is kept as a small example function you can pass in at the boundary. Tests can pass `ignore`.

CLI flags, JSON schema, and CI: [src/docs/AGENT.md](https://github.com/e-St/fspure/blob/main/src/docs/AGENT.md). The skill body is [plugins/fspure/skills/fspure-reduce-impurity/SKILL.md](https://github.com/e-St/fspure/blob/main/plugins/fspure/skills/fspure-reduce-impurity/SKILL.md).
