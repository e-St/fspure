# Nix workflow (bash-free-ish)

## Recommended stack

| Piece | Role |
|-------|------|
| **Flakes** | Single entry for tools + devShell (`flake.nix`) |
| **direnv + nix-direnv** | Auto-load the flake when you `cd` into the repo |
| **Nushell** | Interactive shell / thin task helpers (`src/scripts/fspure.nu`) |
| **F#** | All real logic (`src/DocsGenerator`, `src/DevcontainerGen`, …) |
| **`writeShellApplication`** | Only tiny Nix-packaged shims for `nix run` (find root + `dotnet run`) |

You will not eliminate 100% of bash (Nixpkgs and many CLIs still assume `/bin/sh`). The goal is: **you almost never write bash** — only Nix + F# (and Nushell for interactive glue).

## One-time setup

```bash
# Nix with flakes enabled, then:
nix profile install nixpkgs#direnv nixpkgs#nix-direnv nixpkgs#nushell

# Hook direnv into your login shell (bash/zsh/fish/nu — see direnv docs)
# Example for bash: eval "$(direnv hook bash)"
# Example for nu:   see https://direnv.net/docs/hook.html

cd /path/to/fspure
direnv allow
```

## Daily use

```text
cd fspure/          # direnv loads flake → dotnet, nushell, fspure-docs, …
nu                  # optional interactive shell
use src/scripts/fspure.nu *
fspure docs preview
fspure devcontainer
```

Without entering a subshell:

```text
nix run .#docs -- preview
nix run .#docs -- stable 0.4.0
nix run .#devcontainer
nix run .#devcontainer -- --check
nix run .#info
```

Or plain F# (CI-friendly, no Nix required):

```text
dotnet run --project src/DocsGenerator -- preview
dotnet run --project src/DevcontainerGen
```

## What lives where

| Path | Edit? | Notes |
|------|-------|-------|
| `flake.nix` | yes | packages / apps / devShell |
| `.envrc` | yes | `use flake` + nix-direnv bootstrap |
| `src/DocsGenerator/` | yes | docs orchestration + Scriban (F#) |
| `src/DevcontainerGen/` | yes | fragment merge (F#) |
| `src/scripts/fspure.nu` | yes | interactive Nushell only |
| `src/scripts/*.sh` | avoid | legacy CI; shrink toward F# |
| `writeShellApplication` in flake | keep tiny | root discovery + `exec dotnet run` |

## Policy

1. New tooling logic → **F#** project under `src/`.
2. Expose it as a flake `package` / `app` with a **minimal** `writeShellApplication` if you need `nix run`.
3. Interactive multi-step UX → **Nushell** (`fspure.nu`), not new bash.
4. Do not add Python / C# / Dockerfiles for monorepo tooling.
5. GitHub Actions may call `dotnet run --project …` directly (no Nix required on ubuntu-latest).
