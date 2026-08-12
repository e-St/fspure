# fspure

Agent CLI for [fspure](https://github.com/e-St/fspure).

`fspure analyze` lists **facts**: impure calls inside functions (`caller`, `callee`, range). It does not say what to move.

```bash
dotnet tool install -g fspure
fspure analyze --project MyApp.fsproj --focus src/Core --format json --fail-on-impure
```

Standalone linux-x64 binary (same idea as fstarter `bundlef`):

```bash
cd src/fspure && bundlef -p
./fspure analyze --project MyApp.fsproj --fail-on-impure
```

Schema: [`fspure-analyze.schema.json`](fspure-analyze.schema.json).  
Agent loop and flags: [`src/docs/AGENT.md`](../docs/AGENT.md).

`fspure-collector` remains the IL whitelist tool. This package is `analyze` only.
