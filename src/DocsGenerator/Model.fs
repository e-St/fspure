namespace Fspure.DocsGenerator

open System
open System.IO
open System.Text.Json

/// Values exposed to every Scriban template.
type DocsModel =
    {
        /// stable | preview
        Channel: string
        /// git ref or branch name
        RefName: string
        /// package / docs version label
        Version: string
        /// ISO date of generation (UTC)
        GeneratedAt: string
        /// True when writing into the main branch tree on a stable release
        IsStableRelease: bool
        /// Base URL for this docs set (no trailing slash), e.g. https://fspure.net or …/preview/feat-x
        BaseUrl: string
        /// Absolute monorepo root
        RepoRoot: string
        /// Analyzer package version pin shown in install snippets
        AnalyzerVersion: string
        /// Collector tool version pin
        CollectorVersion: string
        /// Snippet id → source body
        Snippets: Map<string, string>
        /// Pretty-printed Ionide / decoration workspace settings (from vscode-common.json)
        WorkspaceSettingsJson: string
        /// Required-only settings subset for ELI20 path
        MinimalSettingsJson: string
    }

module Model =

    let private readManifestVersions (repoRoot: string) : string * string =
        let path = Path.Combine(repoRoot, "docs", "releases", "manifest.json")

        if not (File.Exists path) then
            "0.4.0", "0.1.0"
        else
            try
                use doc = JsonDocument.Parse(File.ReadAllText path)
                let root = doc.RootElement
                let last = root.GetProperty("lastOfficial")
                let analyzer = last.GetProperty("FSharp.PureAnalyzer").GetString()
                let collector = last.GetProperty("fspure-collector").GetString()
                defaultArg (Option.ofObj analyzer) "0.4.0", defaultArg (Option.ofObj collector) "0.1.0"
            with _ ->
                "0.4.0", "0.1.0"

    let private extractSettings (repoRoot: string) : string * string =
        let path =
            Path.Combine(repoRoot, ".devcontainer", "fragments", "vscode-common.json")

        if not (File.Exists path) then
            "{}", "{}"
        else
            use doc = JsonDocument.Parse(File.ReadAllText path)
            let settings =
                doc.RootElement
                    .GetProperty("customizations")
                    .GetProperty("vscode")
                    .GetProperty("settings")

            let opts = JsonSerializerOptions(WriteIndented = true)
            let full = JsonSerializer.Serialize(settings, opts)

            // Minimal ELI20 set — only what you need for badges to show.
            use minimal = JsonDocument.Parse(
                """
{
  "FSharp.enableAnalyzers": true,
  "FSharp.analyzersPath": [ "analyzers", "packages/Analyzers" ],
  "fsharpPureDecorations.enabled": true,
  "FSharp.lineLens.enabled": "replaceCodeLens",
  "FSharp.lineLens.prefix": "  // ",
  "workbench.colorCustomizations": {
    "editorHint.foreground": "#00000000",
    "editorHint.border": "#00000000",
    "editorOverviewRuler.hintForeground": "#00000000"
  }
}
"""
            )

            let minJson = JsonSerializer.Serialize(minimal.RootElement, opts)
            full, minJson

    let build
        (repoRoot: string)
        (channel: string)
        (refName: string)
        (version: string)
        (baseUrl: string)
        (isStableRelease: bool)
        : DocsModel * string list
        =
        let snippets, warnings = Snippets.collect repoRoot
        let analyzerVer, collectorVer = readManifestVersions repoRoot
        let fullSettings, minSettings = extractSettings repoRoot

        let version' =
            if String.IsNullOrWhiteSpace version then analyzerVer else version

        let model =
            {
                Channel = channel
                RefName = refName
                Version = version'
                GeneratedAt = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'")
                IsStableRelease = isStableRelease
                BaseUrl = baseUrl.TrimEnd('/')
                RepoRoot = repoRoot
                AnalyzerVersion = analyzerVer
                CollectorVersion = collectorVer
                Snippets = snippets
                WorkspaceSettingsJson = fullSettings
                MinimalSettingsJson = minSettings
            }

        model, warnings
