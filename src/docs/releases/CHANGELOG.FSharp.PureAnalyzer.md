# Changelog — FSharp.PureAnalyzer

All notable changes to the NuGet package **FSharp.PureAnalyzer** are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/).

## [Unreleased]

### Draft (from git log — edit freely)

- Add fspure analyze CLI that lists impure calls inside functions. (741abeb)
- Fix analyzer pack path for NOTICE after docs moved to src/docs. (2deee37)
- Restructure repo: editable sources under src/, generated under .generated/ (896b9e3)
- Prefer F# and Nix: drop C#, Python generators, and Dockerfiles. (8813938)
- Minimize monorepo root: move docs, assets, and NOTICE under docs/. (8f42b9a)
- Restructure monorepo into src/tests/editor/docs/scripts. (0fd0a59)

## [0.4.0] — 2026-08-10

### Added

- Phase 3 MSBuild embed targets (`build/`) and bundled `tools/fspure-collector/`
- Library pure.json composition, ProjectReference/PackageReference discovery
- Project overrides via `fspure.overrides.json` and `FSPURE_DISABLE_FOUNDATIONAL`
- Shared `FSharp.PureSchema` dependency next to analyzer DLL

### Changed

- Collector tool renamed to **fspure-collector** (package layout under `tools/fspure-collector/`)

## [0.3.2] — 2026-07-27

### Added

- Initial nuget.org analyzer package (analyzer DLL only; no embed targets)
