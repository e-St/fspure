# Optional: teach `newf` to reference FSharp.PureAnalyzer

Ionide loads PureAnalyzer from the workspace `analyzers/` drop from `setup-fspure.sh`.

To also restore the package on scaffolded projects, add `nuget FSharp.PureAnalyzer`
to `paket.dependencies` and `FSharp.PureAnalyzer` to `paket.references` in `newf.sh`
for backend/CLI modes.
