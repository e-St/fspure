# Sync monorepo sample → `e-St/fspure-ready-lib`

The **source of truth** for the public sample library is:

```text
e-St/fspure   →   samples/fspure-ready-lib/
```

The standalone repo is:

```text
https://github.com/e-St/fspure-ready-lib
```

## How sync works

The workflow **Sync fspure-ready-lib**:

1. Copies `samples/fspure-ready-lib/` into a clone of the satellite (rsync; excludes `bin`/`obj`/`artifacts`).
2. Creates **one synthetic git commit** on the satellite (`sync from e-St/fspure@<sha>`).
3. **`git push`**es to `main`.

That push is a normal GitHub `push` event, so **workflows in the satellite** (CI, embed tests, publish, …) still run.

```mermaid
flowchart LR
  A[Edit sample in fspure] --> B[Push monorepo main]
  B --> C[Sync workflow]
  C --> D[One commit on fspure-ready-lib]
  D --> E[Satellite CI on push]
```

---

## One-time setup (required)

`GITHUB_TOKEN` from the fspure job **cannot** push to another repository.

### Fine-grained PAT

1. Create a fine-grained PAT with access **only** to **`fspure-ready-lib`**:
   - **Contents:** Read and write  
2. In **`e-St/fspure`** → **Settings → Secrets and variables → Actions**:
   - Name: **`FSPURE_READY_LIB_PUSH_TOKEN`**  
   - Value: the PAT  

### Branch protection on the satellite

- Allow the PAT identity to push to `main`, or bypass for that account.
- Prefer editing only in the monorepo; satellite commits from humans can be overwritten on the next sync.

---

## Day-to-day

1. Edit **`samples/fspure-ready-lib/`** in **fspure**.
2. Merge/push to **`main`** on **fspure**.
3. **Sync fspure-ready-lib** runs.
4. **fspure-ready-lib** gets one new commit and runs its **CI**.

Manual: **Actions → Sync fspure-ready-lib → Run workflow** (optional dry_run).

---

## Satellite CI

```yaml
on:
  push:
    branches: [main]
  pull_request:
```

No special webhook is required.

---

## Failure modes

| Symptom | Fix |
|---------|-----|
| Secret not set | Add `FSPURE_READY_LIB_PUSH_TOKEN` on **fspure** |
| `403` on push | PAT needs **Contents: Write** on **fspure-ready-lib** |
| Fork runs sync | Blocked by `if: github.repository == 'e-St/fspure'` |

---

## Security

- Scope the token to **one** repo (`fspure-ready-lib`).
- Store it only as a secret on **fspure**.
