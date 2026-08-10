# Changelog — FSharp.PureAnalyzer

All notable changes to the NuGet package **FSharp.PureAnalyzer** are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/).

## [Unreleased]

<!-- Filled by Prepare release PR from git log — edit freely before merge. -->

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
