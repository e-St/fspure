# Changelog — fspure-reduce-impurity

All notable changes to the Copilot / Claude skill **fspure-reduce-impurity** are documented here.

Official versions are GitHub tags `fspure-reduce-impurity-v{version}`. `gh skill install --pin` uses that tag. fstarter forks keep the pin in `.devcontainer/fspure-versions.env` until they take an update.

## [Unreleased]

### Added

- For F# aficionados and anyone else.

### Changed

- Inject each impurity as a higher-order function argument. Same rewrite for every focused function and every `callee`, not a `printf` / `write` list.
- Name the parameter for the role the effect plays; name the example function for what that call actually does. Those two names must differ.
