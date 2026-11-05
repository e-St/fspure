# Changelog — fspure-reduce-impurity

All notable changes to the Copilot / Claude skill **fspure-reduce-impurity** are documented here.

Official versions are GitHub tags `fspure-reduce-impurity-v{version}`. `gh skill install --pin` uses that tag. fstarter forks keep the pin in `.devcontainer/fspure-versions.env` until they take an update.

## [Unreleased]

## [0.1.3] — 2026-11-05

### Draft (from git log — edit freely)

- chore(release): apply official version pins after publish (62a0a95)
- chore(release): apply official version pins after publish (9458d7a)
- Say the skill is for F# aficionados and anyone else. (216f178)
- Release the skill like the analyzer: version, changelog, and PR. (0a282f6)
- Make impurity injection apply to every callee, not a name list. (3f3951c)
- Name extracted effects factually, not after the HOF parameter. (e060f86)
- Define example effect functions; use generic HOF parameter names. (0b559ec)
- Inject impurity as a function argument so cores stay testable. (c20fe1e)
- Use idiomatic F# when splitting out deferred effects. (d3ed280)
- Keep composition examples as comments, not live calls. (7347fc6)
- Extract deferred effects into impure functions, do not hoist. (b9c1f68)
- Tell the skill to relocate effects, never drop them. (fc7cd21)
- Put fspure on PATH and auto-approve the analyze loop. (397c5e0)
- Fix non-interactive Copilot skill install from fstarter. (3dec78c)
- Publish fspure-reduce-impurity as a marketplace skill, not repo copies. (c70dba5)

## [0.1.2] — 2026-11-05

### Draft (from git log — edit freely)

- chore(release): apply official version pins after publish (9458d7a)
- Say the skill is for F# aficionados and anyone else. (216f178)
- Release the skill like the analyzer: version, changelog, and PR. (0a282f6)
- Make impurity injection apply to every callee, not a name list. (3f3951c)
- Name extracted effects factually, not after the HOF parameter. (e060f86)
- Define example effect functions; use generic HOF parameter names. (0b559ec)
- Inject impurity as a function argument so cores stay testable. (c20fe1e)
- Use idiomatic F# when splitting out deferred effects. (d3ed280)
- Keep composition examples as comments, not live calls. (7347fc6)
- Extract deferred effects into impure functions, do not hoist. (b9c1f68)
- Tell the skill to relocate effects, never drop them. (fc7cd21)
- Put fspure on PATH and auto-approve the analyze loop. (397c5e0)
- Fix non-interactive Copilot skill install from fstarter. (3dec78c)
- Publish fspure-reduce-impurity as a marketplace skill, not repo copies. (c70dba5)

## [0.1.1] — 2026-08-12

### Added

- For F# aficionados and anyone else.

### Changed

- Inject each impurity as a higher-order function argument. Same rewrite for every focused function and every `callee`, not a `printf` / `write` list.
- Name the parameter for the role the effect plays; name the example function for what that call actually does. Those two names must differ.
