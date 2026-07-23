# Copilot Instructions

## Package management

Always use **paket** to install, remove, or update NuGet packages — never `dotnet add package`.

- Add a package: edit `paket.dependencies` to add the `nuget <Package> [version]` line, then run `paket update`.
- Reference it in a project: ensure `paket.references` in that project lists the package, then run `paket install`.
- Remove a package: remove the line from `paket.dependencies` (and `paket.references`), then run `paket install`.
- The paket binary is available globally as `paket`.
