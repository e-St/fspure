# Optional Nix shell

Nix is **not** required for development. Prefer F#:

```text
dotnet run --project src/Fspure.Tasks -- docs preview
dotnet run --project src/Fspure.Tasks -- security
```

The flake only provides a .NET SDK + thin `writeShellApplication` wrappers that `exec` those F# projects.

```text
nix develop          # SDK + fspure / fspure-docs / … on PATH
nix run .#fspure -- docs preview
nix run .#security
```

direnv: `.envrc` → `use flake` (optional).

See [LANGUAGES.md](LANGUAGES.md).
