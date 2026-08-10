# fspure architecture (purity story)

Short note on how pure/impure labels are decided for **vanilla fspure** (analyzer + VS Code extension).

## Pieces

| Piece | Role |
|-------|------|
| **purity-collector** | Scans managed assemblies (IL) and writes a `PureFile` (`.pure.json`) whitelist |
| **FSharp.PureAnalyzer** | F# analyzer: builds a pure set, classifies definitions (`PURE002` impure / `PURE003` pure) |
| **fsharp-pure-decorations** | VS Code: turns `PURE002`/`PURE003` into end-of-line badges |
| **MSBuild targets** (in the analyzer package) | After build: collect → optional `pure-extra.json` merge → embed `{AssemblyName}.pure.json` into the library DLL |

## Discovery order and precedence

When analysing a project, the pure set is composed as follows:

```text
  base = foundational.pure.json   (unless disabled)
           │
           ▼
  + library embeds from referenced DLLs  (*.pure.json resources)
           │
           ▼
  ± fspure.overrides.json   (remove, then add)
```

**Fixed precedence (highest last):**

1. **Foundational** — embedded `foundational.pure.json` + small hard-coded F# operator / `FSharpFunc.Invoke` safety nets  
2. **Library embeds** — pure manifests inside referenced assemblies (ProjectReference or PackageReference)  
3. **Overrides** — project-local `fspure.overrides.json` (add/remove)

So: **overrides > library embeds > foundational**.

### Disabling foundational

Power users who want only their own lists:

- In `fspure.overrides.json`: `"useFoundational": false`  
- Or environment: `FSPURE_DISABLE_FOUNDATIONAL=1` (also `true` / `yes` / `on`)

Operator / invoke safety nets in the analyser remain (so `|>` etc. still behave); the large foundational name list is not loaded.

### Overrides file

Place **`fspure.overrides.json`** next to the `.fsproj` (or set `FSPURE_OVERRIDES_PATH`).

```json
{
  "schemaVersion": "1.0",
  "useFoundational": true,
  "add": [ "MyCompany.Math.SecretPure" ],
  "remove": [ "Some.Method.I.Do.Not.Trust" ]
}
```

JSON Schema: `schema/FSharp.PureSchema/fspure.overrides.schema.json`.

Invalid override files are ignored (analysis never fails because of them).

## Library authors (fspure-ready)

```xml
<PackageReference Include="FSharp.PureAnalyzer" Version="VERSION" PrivateAssets="all" />
```

Requires a package that ships **Phase 3** layout (`build/FSharp.PureAnalyzer.targets` + `tools/purity-collector/`).  
See [samples/fspure-ready-lib](../samples/fspure-ready-lib/).

Optional author merge file next to the library project: **`pure-extra.json`** (same PureFile shape as collector output).

## Consumers (vanilla)

1. Reference `FSharp.PureAnalyzer` (analyzer DLL under `analyzers/dotnet/fs/`)  
2. Optional: VS Code extension for badges  
3. Optional: `fspure.overrides.json` in the **app** project  

No need to fork the analyzer to tweak the pure list (scenario 3).

## Regression gates

| Gate | Script / workflow |
|------|-------------------|
| Library embed (local feed) | `scripts/fspure-ready-lib-gate.sh` |
| Full purity story | `scripts/phase5-regression.sh` |
| Per-phase CIs | `.github/workflows/pure-*.yml` |

## TFM

All new purity infrastructure targets **net10.0** only.
