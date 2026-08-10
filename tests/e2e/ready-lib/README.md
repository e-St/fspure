# e2e: fspure-ready-lib (library embed)

Proves scenario 2 with a **real nupkg**, without publishing to nuget.org:

1. Pack monorepo `FSharp.PureAnalyzer` → `artifacts/local-feed/`
2. Pack `samples/fspure-ready-lib` against that package (embeds `pure.json`)
3. Consumer restores the ReadyLib nupkg from the local feed
4. `fsharp-analyzers` must classify:
   - `Consumer.useAdd` → **PURE003** (only if the library embed was loaded)
   - `Consumer.useImpure` → **PURE002**

```bash
# From fspure repo root
bash tests/e2e/ready-lib/run.sh
# same as:
bash scripts/fspure-ready-lib-gate.sh
```

Artifacts: `artifacts/fspure-ready-lib-gate/` (SARIF, stdout, nupkgs).
