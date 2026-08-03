#!/usr/bin/env python3
"""Generate end-user customer.md from shared devcontainer fragments.

Keeps consumer install docs aligned with the settings we ship in
.devcontainer/fragments/vscode-common.json.

Usage:
  python3 .devcontainer/generate-customer-md.py
  python3 .devcontainer/generate-customer-md.py --check
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
VSCODE_COMMON = HERE / "fragments" / "vscode-common.json"
OUTPUT = ROOT / "customer.md"

BANNER = (
    "<!-- GENERATED FILE — do not edit by hand.\n"
    "     Source: .devcontainer/fragments/vscode-common.json\n"
    "     Regenerate: python3 .devcontainer/generate-customer-md.py -->\n\n"
)

# Settings that must be present for pure/impure badges to work with Ionide.
REQUIRED_SETTING_KEYS = {
    "FSharp.enableAnalyzers",
    "FSharp.analyzersPath",
    "fsharpPureDecorations.enabled",
}

# Strongly recommended for readable badges (LineLens + hide grey hints).
RECOMMENDED_SETTING_KEYS = {
    "FSharp.inlayHints.typeAnnotations",
    "FSharp.inlayHints.parameterNames",
    "FSharp.inlayHints.enabled",
    "editor.inlayHints.enabled",
    "FSharp.lineLens.enabled",
    "FSharp.lineLens.prefix",
    "workbench.colorCustomizations",
}


def load_settings() -> dict:
    doc = json.loads(VSCODE_COMMON.read_text(encoding="utf-8"))
    return doc["customizations"]["vscode"]["settings"]


def load_extensions() -> list[str]:
    doc = json.loads(VSCODE_COMMON.read_text(encoding="utf-8"))
    return list(doc["customizations"]["vscode"]["extensions"])


def subset(settings: dict, keys: set[str]) -> dict:
    # Stable key order for reproducible customer.md / --check.
    return {k: settings[k] for k in sorted(keys) if k in settings}


def json_block(obj: object) -> str:
    return "```json\n" + json.dumps(obj, indent=2, ensure_ascii=False) + "\n```\n"


def render(settings: dict, extensions: list[str]) -> str:
    required = subset(settings, REQUIRED_SETTING_KEYS)
    recommended = subset(settings, RECOMMENDED_SETTING_KEYS)
    optional = {
        k: settings[k]
        for k in sorted(settings)
        if k not in REQUIRED_SETTING_KEYS and k not in RECOMMENDED_SETTING_KEYS
    }
    if "fsharpPureDecorations.impureColor" not in optional:
        optional["fsharpPureDecorations.impureColor"] = "#E2A66A"
        optional["fsharpPureDecorations.pureColor"] = "#6A9955"
        optional = {k: optional[k] for k in sorted(optional)}

    full_workspace = {k: required[k] for k in sorted(required)}
    full_workspace.update({k: recommended[k] for k in sorted(recommended)})

    # Opinionated devcontainer path: entire vscode-common stack + solution keys.
    opinionated_settings = {k: settings[k] for k in sorted(settings)}
    opinionated_settings["dotnet.defaultSolution"] = "YourSolution.sln"
    opinionated_settings["FSharp.workspacePath"] = "YourSolution.sln"

    opinionated_extensions = list(
        dict.fromkeys(
            [
                "ionide.ionide-fsharp",
                "e-st.fsharp-pure-decorations",
                *extensions,
            ]
        )
    )

    lines: list[str] = []
    a = lines.append

    a("# Using fspure in your project\n")
    a(
        "This guide is for **end users** who want pure/impure labels in the editor. "
        "It is not for contributors to the fspure repository.\n"
    )
    a("For the full IDE experience you need **both**:\n")
    a("1. **FSharp.PureAnalyzer** (NuGet) — classifies definitions (`PURE002` / `PURE003`)\n")
    a(
        "2. **fsharp-pure-decorations** (VS Code extension) — shows end-of-line "
        "**pure** / **impure** badges after Ionide LineLens\n"
    )
    a(
        "Plus **Ionide for F#** (language service that loads the analyzer). "
        "How you get them depends on which path you pick:\n"
    )
    a("| Path | When to use |\n")
    a("|------|-------------|\n")
    a(
        "| **§0 e-St/fstarter** | You want an opinionated F# Codespace / dev container "
        "that already includes fspure |\n"
    )
    a("| **§1 No dev container** | Local VS Code / desktop IDE; you wire NuGet + extension yourself |\n")
    a(
        "| **§2 Your own dev container** | You already have (or want) a project "
        "`.devcontainer/` and will add fspure to it |\n"
    )

    # ------------------------------------------------------------------
    a("## 0. Use e-St/fstarter (recommended if you want zero setup)\n")
    a(
        "[**e-St/fstarter**](https://github.com/e-St/fstarter) is an opinionated F# "
        "Codespace / dev-container starter. It already delivers the F# toolchain "
        "(Ionide, .NET, Paket, etc.) **and fspure** (analyzer + pure/impure decorations) "
        "so you do not install packages or extensions by hand for labels to work.\n"
    )
    a("### What you do\n")
    a(
        "1. Open or create a project from "
        "[e-St/fstarter](https://github.com/e-St/fstarter) "
        "(GitHub Codespaces “Open in codespace”, or clone and "
        "“Reopen in Container”).\n"
    )
    a("2. Work on your F# code inside that environment.\n")
    a(
        "3. Open a solution / `.fs` file and wait for Ionide — pure/impure badges "
        "should appear without further fspure configuration.\n"
    )
    a("### When this is the right choice\n")
    a("- You are starting greenfield F# work and are fine with the fstarter defaults.\n")
    a("- You want Codespaces / a full F# container, not a minimal local install.\n")
    a(
        "- You do not want to maintain NuGet paths, extension installs, or Ionide "
        "settings yourself.\n"
    )
    a("### When to use §1 or §2 instead\n")
    a("- Your app already lives in its own repo with its own tooling and you only want fspure added (§1 or §2).\n")
    a("- You cannot use GitHub Codespaces / that base image (air-gapped, corporate base image, etc.).\n")
    a(
        "Details of what fstarter pins (image tags, setup scripts) live in the "
        "[fstarter repository](https://github.com/e-St/fstarter) — treat that as the "
        "source of truth for the starter itself.\n"
    )

    # ------------------------------------------------------------------
    a("## 1. Usage without a dev container\n")
    a("### 1.1 Install the analyzer (NuGet)\n")
    a("```bash\n")
    a("dotnet add package FSharp.PureAnalyzer\n")
    a("```\n")
    a("Or with Paket:\n")
    a("```\n")
    a("nuget FSharp.PureAnalyzer\n")
    a("```\n")
    a(
        "The package ships the analyzer under `analyzers/dotnet/fs/`. "
        "Ionide’s FSAC must see a **real directory** via `FSharp.analyzersPath` "
        "(it does **not** expand `~`, `${userHome}`, or other VS Code variables).\n"
    )
    a("Typical approaches:\n")
    a(
        "- Point `FSharp.analyzersPath` at a workspace folder such as `analyzers` "
        "and copy/symlink the package’s `analyzers/dotnet/fs/` tree there, or\n"
    )
    a(
        "- Use an **absolute** path to the installed package under your NuGet "
        "global packages folder.\n"
    )

    a("### 1.2 Install the VS Code extension\n")
    a(
        "The extension is published to [Open VSX](https://open-vsx.org/) as "
        "`e-st.fsharp-pure-decorations` (**F# Pure Analyzer Decorations**).\n"
    )
    a(
        "- **Open VSX clients** (VSCodium, many code-server setups, Cursor with Open VSX): "
        "search and install from the marketplace.\n"
    )
    a(
        "- **Stock VS Code** (Microsoft Marketplace): install a `.vsix` from "
        "[GitHub Releases](https://github.com/e-St/fspure/releases), or configure Open VSX.\n"
    )
    a("```bash\n")
    a("code --install-extension e-st.fsharp-pure-decorations\n")
    a("# or from a downloaded VSIX:\n")
    a("code --install-extension fsharp-pure-decorations-*.vsix\n")
    a("```\n")
    a("Also install **Ionide for F#** (`ionide.ionide-fsharp`) if you do not already use it.\n")

    a("### 1.3 Workspace settings\n")
    a("Add (or merge) into `.vscode/settings.json`.\n")
    a("#### Required\n")
    a(json_block(required))
    a("#### Recommended\n")
    a(
        "These make LineLens signatures and pure/impure badges readable "
        "(badges sit after `// signature`; grey diagnostic hint text is hidden).\n"
    )
    a(json_block(recommended))
    a("#### Combined minimum (required + recommended)\n")
    a(json_block(full_workspace))
    a("#### Optional\n")
    a(
        "Useful editor UX from our reference setup; not required for classification "
        "or badges. Decorations colors default to impure orange / pure green.\n"
    )
    a(json_block(optional))

    a("### 1.4 Open your solution\n")
    a(
        "Open the solution or project, open an `.fs` file, wait for Ionide to load. "
        "You should see LineLens signatures (`// …`) and **pure** / **impure** badges "
        "on definitions. If labels are missing: **Developer: Reload Window**, "
        "and confirm the analyzer DLL is under a path listed in `FSharp.analyzersPath`.\n"
    )

    # ------------------------------------------------------------------
    a("## 2. Usage with a dev container (opinionated)\n")
    a(
        "If you develop in a [dev container](https://containers.dev/), follow this "
        "**one recipe**. It is the full recommended IDE stack for pure/impure labels — "
        "not a menu of options. Required / recommended / optional settings are already "
        "listed in §1; this section only tells you what to put in **your** "
        "`.devcontainer/`.\n"
    )
    a(
        "You do **not** need this repository’s internal IDE, build, or e2e containers.\n"
    )

    a("### 2.1 What you commit\n")
    a("1. `FSharp.PureAnalyzer` as a normal NuGet or Paket dependency on your F# project.\n")
    a(
        "2. `.devcontainer/devcontainer.json` — use the template below "
        "(only change the two solution path strings).\n"
    )
    a("3. `.devcontainer/setup-fspure.sh` — use the script below as-is.\n")
    a(
        "Regenerate `analyzers/dotnet/fs/FSharp.PureAnalyzer.dll` (and "
        "`FSharp.PureSchema.dll`) on every create/attach via the setup script; "
        "you normally do **not** commit that drop.\n"
    )

    a("### 2.2 `.devcontainer/devcontainer.json`\n")
    a(
        "Swap in your base image if you already have one. Keep "
        "`postCreateCommand`, `postAttachCommand`, and `customizations.vscode` as shown. "
        "Replace `YourSolution.sln` with your solution (or `.slnx`).\n"
    )
    skeleton = {
        "name": "My F# app + fspure",
        "image": "mcr.microsoft.com/devcontainers/dotnet:1-10.0-noble",
        "remoteUser": "vscode",
        "postCreateCommand": "bash .devcontainer/setup-fspure.sh",
        "postAttachCommand": "bash .devcontainer/setup-fspure.sh",
        "customizations": {
            "vscode": {
                "extensions": opinionated_extensions,
                "settings": opinionated_settings,
            }
        },
    }
    a(json_block(skeleton))

    a("### 2.3 `.devcontainer/setup-fspure.sh`\n")
    a(
        "Runs on create and attach: installs the decorations extension when `code` is "
        "available, and mirrors the restored NuGet analyzer into workspace "
        "`analyzers/` so `FSharp.analyzersPath` works (FSAC does not expand home/`~`).\n"
    )
    a(
        "```bash\n"
        "#!/usr/bin/env bash\n"
        "set -euo pipefail\n"
        "\n"
        "# Codespaces uses MS Marketplace — extension is on Open VSX only.\n"
        "# Install from downloaded VSIX (not marketplace id alone).\n"
        "if command -v code >/dev/null 2>&1; then\n"
        "  vsix=$(mktemp --suffix=.vsix)\n"
        "  url=$(curl -fsSL https://open-vsx.org/api/e-St/fsharp-pure-decorations/latest \\\n"
        "    | python3 -c \"import json,sys; print(json.load(sys.stdin)['files']['download'])\")\n"
        "  curl -fsSL -o \"$vsix\" \"$url\"\n"
        "  code --install-extension \"$vsix\" --force\n"
        "  rm -f \"$vsix\"\n"
        "fi\n"
        "\n"
        "# Mirror NuGet analyzer + PureSchema into workspace-relative analyzers/ for FSAC.\n"
        "PKG=\"${NUGET_PACKAGES:-$HOME/.nuget/packages}/fsharp.pureanalyzer\"\n"
        "DLL=\"$(find \"$PKG\" -path '*/analyzers/dotnet/fs/FSharp.PureAnalyzer.dll' \\\n"
        "  2>/dev/null | sort -V | tail -1 || true)\"\n"
        "if [[ -z \"${DLL}\" || ! -f \"${DLL}\" ]]; then\n"
        "  echo \"FSharp.PureAnalyzer DLL not found under $PKG — restore the package first.\" >&2\n"
        "  exit 1\n"
        "fi\n"
        "SCHEMA=\"$(dirname \"$DLL\")/FSharp.PureSchema.dll\"\n"
        "mkdir -p analyzers/dotnet/fs\n"
        "cp -f \"$DLL\" analyzers/dotnet/fs/FSharp.PureAnalyzer.dll\n"
        "if [[ -f \"$SCHEMA\" ]]; then\n"
        "  cp -f \"$SCHEMA\" analyzers/dotnet/fs/FSharp.PureSchema.dll\n"
        "else\n"
        "  echo \"WARNING: FSharp.PureSchema.dll missing next to analyzer (older package?).\" >&2\n"
        "fi\n"
        "echo \"PureAnalyzer → analyzers/dotnet/fs/FSharp.PureAnalyzer.dll\"\n"
        "```\n"
    )
    a(
        "Make it executable (`chmod +x .devcontainer/setup-fspure.sh`). "
        "Restore your project (so the NuGet package is present) before or at the "
        "start of this script. After attach: open the solution, open an `.fs` file, "
        "wait for Ionide. If badges are missing, **Developer: Reload Window**.\n"
    )

    a("---\n")
    a("## See also\n")
    a("- [README — consume fspure](README.md#consume-fspure-end-users)\n")
    a("- [FSharp.PureAnalyzer](FSharp.PureAnalyzer/README.md)\n")
    a("- [VS Code extension](vscode-extension/README.md)\n")
    a("- Maintainer publishing: [docs/PUBLISHING.md](docs/PUBLISHING.md)\n")

    return BANNER + "".join(lines)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--check",
        action="store_true",
        help="exit 1 if customer.md is missing or out of date",
    )
    args = parser.parse_args(argv)

    if not VSCODE_COMMON.is_file():
        print(f"ERROR: missing {VSCODE_COMMON}", file=sys.stderr)
        return 1

    settings = load_settings()
    extensions = load_extensions()
    text = render(settings, extensions)

    if args.check:
        if not OUTPUT.is_file():
            print(
                f"ERROR: missing {OUTPUT}; run generate-customer-md.py",
                file=sys.stderr,
            )
            return 1
        actual = OUTPUT.read_text(encoding="utf-8")
        if actual != text:
            print(
                "ERROR: customer.md is out of date.\n"
                "Run: python3 .devcontainer/generate-customer-md.py",
                file=sys.stderr,
            )
            return 1
        print("OK: customer.md is up to date.")
        return 0

    OUTPUT.write_text(text, encoding="utf-8")
    print(f"wrote {OUTPUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
