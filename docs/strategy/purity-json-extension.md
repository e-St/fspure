# Strategy: PureAnalyzer + third-party `purity.json`

**Status:** Design / implementation guide  
**Related:** [Library author user guide](../library-authors/USER_GUIDE.md)

## 1. Goals

| Goal | Detail |
|------|--------|
| Default purity | PureAnalyzer continues to use the **embedded foundational** pure set (BCL + FSharp.Core surface + supplements). |
| Third-party purity | If a **DLL referenced by the project** offers a **`purity.json`**, PureAnalyzer **includes** those pure methods in leaf checks. |
| No source annotations | Library authors do **not** add `[Pure]` in hand-written code. |
| Author workflow | Authors run **purity-collector** (or equivalent) in CI and ship the result with the library. |
| Safe by default | Unknown external members remain **impure** leaves. Malicious pure lists cannot whitewash other assemblies. |

Non-goals (v1):

- Whole-program purity across packages without leaves.  
- Inferring purity for packages that ship no `purity.json`.  
- Replacing foundational generation (List A remains fspure’s responsibility).

---

## 2. Current architecture (baseline)

```text
FCS project implementation
    → Analysis: call graph + non-local mutation
    → isPure(name):
         nonLocalMutation? → impure
         PureSet.contains(name)? → pure leaf
         all callees pure? → pure
         unknown external / missing edge → impure leaf
```

`PureSet` today:

- Loads **one** embedded resource: `foundational.pure.json`.  
- Builds exact / normalized / last-segment indexes.  
- Adds hard-coded supplemental FSharp.Core leaves.  
- **No** scan of referenced assemblies.

Cross-project / NuGet library calls therefore only count as pure if they already appear in that foundational index.

---

## 3. Target architecture

```text
                    ┌─────────────────────────────┐
                    │ foundational.pure.json      │  (always)
                    │ + supplementalLeaves        │
                    └─────────────┬───────────────┘
                                  │
Referenced assemblies ──► discover purity.json ──► validate ──► union
                                  │
                    ┌─────────────▼───────────────┐
                    │ PureSet (merged index)      │
                    └─────────────┬───────────────┘
                                  │
                         Analysis.isPure (unchanged shape)
```

### 3.1 Discovery order (per referenced assembly)

Given assembly path `A.dll` (or loadable `Assembly`):

1. **Embedded resource** (preferred)  
   - Any manifest resource name ending with `purity.json` (case-insensitive), or logical name exactly `purity.json`.  
2. **Sidecar file**  
   - `DirectoryName(A.dll)/purity.json` if the file exists.  
3. **Optional NuGet fallback** (later)  
   - From `project.assets.json` / global packages folder:  
     `$NUGET_PACKAGES/{id}/{version}/lib/**/purity.json`  
   - Only if (1)–(2) fail and package identity is known.

Skip framework assemblies that are already covered by foundational data if desired (optimization), but loading an extra empty/missing resource is cheap.

### 3.2 Merge semantics

- Start with foundational + supplements (today’s index).  
- For each discovered document:  
  - Parse with existing DTO shape (`schemaVersion`, `packageId`, `pureMethods`, …).  
  - If `schemaVersion` unsupported → log once, skip file.  
  - Filter methods: **only names attributable to this assembly** (see §4).  
  - Add remaining names to the same exact/normalized/last-segment indexes.  
- Last write wins on duplicate names (or first wins—pick one and document; prefer **first foundational, then packages in stable reference order** so foundational cannot be overridden by a package).

**Recommendation:** foundational entries are never removed; package lists only **add** leaves. If a package claims a foundational name, ignore that claim.

### 3.3 When to load

| Host | Trigger |
|------|---------|
| Ionide / FSAC analyzer | Per analysis run (or cache keyed by project + assembly paths + file mtimes / package versions). |
| `fsharp-analyzers` CLI | Same, once per project invocation. |

Cache invalidation: change of any referenced assembly path, `purity.json` mtime, or package version.

---

## 4. Trust boundary

Without this, any package could mark `System.IO.File.Delete` pure.

**Rules:**

1. A `purity.json` discovered **from assembly A** may only contribute names that resolve to **A** (assembly simple name / module prefix agreement).  
2. Heuristic v1 (pragmatic):  
   - Normalize `fullName`.  
   - Accept if it starts with a namespace prefix declared in the document’s `packageId` **or** matches the assembly’s simple name as a segment **or** appears as a type defined in A (if reflection available).  
3. Reject / drop entries pointing at `System.`, `Microsoft.FSharp.`, etc., unless assembly A **is** that framework assembly (it won’t be for third parties).  
4. Cap list size (e.g. 50k entries) to avoid pathological packages.

---

## 5. Implementation plan (code touch points)

### Phase A — Multi-source `PureSet` (no FCS discovery yet)

**Files:** `FSharp.PureAnalyzer/PureSet.fs`, tests.

- Split `parsedIndex` into:  
  - `foundationalIndex` (lazy, current load).  
  - `mergedIndex` built from foundational + `IReadOnlyList<PureFileDto>` or raw name sequences.  
- API sketch:

```fsharp
module PureSet =
    // existing
    val contains: string -> bool

    // new
    val resetExternal: unit -> unit
    val addExternalPureFile: PureFileDto -> unit
    // or:
    val withExternalFiles: seq<PureFileDto> -> (* context *) 
```

Ionide analyzers are often process-long-lived: prefer **immutable merge per analysis context** over mutable global state if the analyzer API allows passing context. If today’s analyzer is static, use a process-wide cache keyed by project id + fingerprint.

**Tests:** unit tests with in-memory JSON strings; merge + filter + contains.

### Phase B — Discover from assembly paths

**Files:** new `PurityDiscovery.fs`, wired from `Analyzer.fs` / analysis entry.

```fsharp
module PurityDiscovery =
    val tryLoadFromAssemblyPath: path: string -> PureFileDto option
    val loadFromAssemblyPaths: paths: string list -> PureFileDto list
```

Implementation:

- `Assembly.ReflectionOnlyLoadFrom` / `MetadataLoadContext` (prefer **MetadataLoadContext** to avoid locking and dependency hell) **or** only filesystem sidecar + `System.Reflection.Metadata` for resources without executing code.  
- Prefer **System.Reflection.Metadata** / PE reader to read manifest resources without loading the assembly into the default ALC.

### Phase C — Obtain referenced assembly paths from FCS

**Files:** `Analyzer.fs`, possibly `Analysis.fs`.

From the F# checker / project options for the analyzed project:

- Collect referenced assembly file names (`OtherOptions` often includes `-r:path`).  
- Also project-reference outputs when present.  
- Pass that list into discovery before computing purity for the implementation files.

If paths are incomplete under Ionide, Phase D fallback is required for NuGet.

### Phase D — NuGet cache fallback (optional but practical)

Parse `project.assets.json` next to the project (or walk upward) for package folders; locate `purity.json` under each package’s `lib/**`.

### Phase E — Diagnostics / observability (optional)

- Analyzer option or verbose log: “loaded purity.json from X (N methods)”.  
- Hidden diagnostic or log when JSON invalid / filtered to empty.

### Phase F — Docs + templates

Already started under `docs/library-authors/`. Keep USER_GUIDE in sync with discovery behavior when shipping.

---

## 6. API / behavior changes (user-visible)

| Before | After |
|--------|--------|
| Only foundational pure leaves | Foundational ∪ referenced assemblies’ `purity.json` |
| Cross-package pure APIs look impure | Pure if listed in that package’s `purity.json` and trusted |
| No author packaging story | Documented embed + CI generate |

**Backward compatible:** packages without `purity.json` behave exactly as today.

---

## 7. Testing strategy

| Layer | Cases |
|-------|--------|
| Parse | Valid 1.0 document; unknown schemaVersion; empty methods |
| Trust filter | Drop `System.IO.File.ReadAllText`; keep `MyLib.Foo.bar` |
| Merge | Foundational + two packages; no override of foundational |
| Discovery | Embedded resource; sidecar; missing file |
| Integration | Fixture project references a tiny lib DLL with embedded `purity.json`; definition calling pure lib API is PURE003; calling unlisted API remains impure |

Add `e2e` or unit fixtures under `FSharp.PureAnalyzer.Tests` (or extend existing tests) with a prebuilt mini assembly if needed.

---

## 8. Rollout

1. Land docs + templates (this branch).  
2. Implement Phase A + B + unit tests; ship analyzer that loads pure sets from **explicit test paths** or env for dogfooding.  
3. Phase C: wire FCS `-r:` paths.  
4. Publish PureAnalyzer minor version; announce library author guide.  
5. Phase D if Ionide path gaps appear in the wild.  
6. Optionally publish `purity-collector` as a global tool for the workflow template.

---

## 9. Open decisions

| Topic | Options | Proposal |
|-------|---------|----------|
| Resource name | `purity.json` only vs also `*.pure.json` | **`purity.json`** primary; accept `*.pure.json` suffix as alias |
| Override foundational | allow / deny | **Deny** package overrides of foundational names |
| Mutable global PureSet | process cache vs per-run | **Per analysis fingerprint cache** |
| Load mechanism | Reflection vs Metadata | **System.Reflection.Metadata** PE read |

---

## 10. Summary

Extend PureAnalyzer so `PureSet` = **foundational (default)** ∪ **all valid `purity.json` documents found on DLLs included in the project**. Generation stays purity-collector-like; shipping is embed/sidecar; no hand-written purity attributes. Implementation is layered: merge layer → filesystem/resource discovery → FCS reference paths → optional NuGet fallback.
