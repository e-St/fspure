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

# Nice-to-have / UX (from our IDE flavour; not required for classification).
OPTIONAL_SETTING_KEYS = {
    "editor.inlineSuggest.enabled",
    "editor.parameterHints.enabled",
    "editor.acceptSuggestionOnEnter",
    "[fsharp]",
    "FSharp.enableMSBuildProjectGraph",
    "FSharp.linter",
    "FSharp.unusedDeclarationsAnalyzer",
    "FSharp.codeLenses.references.enabled",
    "editor.formatOnSave",
    "files.exclude",
    "fsharpPureDecorations.impureColor",
    "fsharpPureDecorations.pureColor",
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
    # Everything else from vscode-common that is not required
    optional = {
        k: settings[k]
        for k in sorted(settings)
        if k not in REQUIRED_SETTING_KEYS and k not in RECOMMENDED_SETTING_KEYS
    }
    # Also surface decoration colors as optional even if not in vscode-common
    if "fsharpPureDecorations.impureColor" not in optional:
        optional["fsharpPureDecorations.impureColor"] = "#E2A66A"
        optional["fsharpPureDecorations.pureColor"] = "#6A9955"
        optional = {k: optional[k] for k in sorted(optional)}

    # Full “copy this” settings for convenience (stable key order)
    full_workspace = {k: required[k] for k in sorted(required)}
    full_workspace.update({k: recommended[k] for k in sorted(recommended)})

    # Extensions for customers: Ionide + pure decorations (C#/Paket/Fantomas optional)
    customer_required_ext = [
        "ionide.ionide-fsharp",
        "e-st.fsharp-pure-decorations",
    ]
    customer_optional_ext = [
        e for e in extensions if e not in ("ionide.ionide-fsharp",)
    ]

    lines: list[str] = []
    a = lines.append

    a("# Using fspure in your project\n")
    a(
        "This guide is for **end users** who want pure/impure labels in the editor. "
        "It is not for contributors to the fspure repository.\n"
    )
    a("You need **both**:\n")
    a("1. **FSharp.PureAnalyzer** (NuGet) — classifies definitions (`PURE002` / `PURE003`)\n")
    a(
        "2. **fsharp-pure-decorations** (VS Code extension) — shows end-of-line "
        "**pure** / **impure** badges after Ionide LineLens\n"
    )
    a(
        "Plus **Ionide for F#** (language service that loads the analyzer).\n"
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
    a("- **Open VSX clients** (VSCodium, many code-server setups, Cursor with Open VSX): search and install from the marketplace.\n")
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
        "or badges. Decoration colors default to impure orange / pure green.\n"
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
    a("## 2. Usage with a dev container\n")
    a(
        "Use this when your project already (or will) develop inside a "
        "[Development Container](https://containers.dev/). "
        "You add fspure pieces to **your** `devcontainer.json` — you do not need "
        "this repository’s internal IDE/build/e2e containers.\n"
    )

    a("### 2.1 Extensions\n")
    a("Under `customizations.vscode.extensions`, install at least:\n")
    a(json_block(customer_required_ext))
    a("Optional companion extensions we use in reference setups:\n")
    a(json_block(customer_optional_ext))
    a(
        "If `e-st.fsharp-pure-decorations` is not on the Marketplace your client uses, "
        "install the VSIX in `postCreateCommand` (see below) instead of listing the id.\n"
    )

    a("### 2.2 Settings\n")
    a(
        "Put the same **required + recommended** settings under "
        "`customizations.vscode.settings` (or in a workspace `.vscode/settings.json` "
        "mounted into the container).\n"
    )
    a("Example `customizations` block (replace `YourSolution.sln`):\n")

    customizations = {
        "vscode": {
            "extensions": customer_required_ext + customer_optional_ext,
            "settings": {
                **full_workspace,
                "dotnet.defaultSolution": "YourSolution.sln",
                "FSharp.workspacePath": "YourSolution.sln",
            },
        }
    }
    a(json_block({"customizations": customizations}))

    a("### 2.3 Analyzer package in the container\n")
    a(
        "Restore/add the NuGet package as part of your normal project restore, "
        "**and** ensure Ionide can load the DLL from a workspace-relative path.\n"
    )
    a("Minimal `postCreateCommand` sketch:\n")
    a("```bash\n")
    a("# After your project restore (dotnet/paket):\n")
    a("dotnet add path/to/YourProject.fsproj package FSharp.PureAnalyzer\n")
    a("\n")
    a("# Mirror analyzer into workspace so FSharp.analyzersPath: [\"analyzers\"] works:\n")
    a('PKG="$HOME/.nuget/packages/fsharp.pureanalyzer"\n')
    a('DLL="$(find "$PKG" -path \'*/analyzers/dotnet/fs/FSharp.PureAnalyzer.dll\' \\\n')
    a('  2>/dev/null | sort -V | tail -1)"\n')
    a('mkdir -p analyzers/dotnet/fs\n')
    a('cp -f "$DLL" analyzers/dotnet/fs/FSharp.PureAnalyzer.dll\n')
    a("```\n")
    a(
        "Alternatively, install from Open VSX / VSIX in the same script:\n"
    )
    a("```bash\n")
    a("code --install-extension e-st.fsharp-pure-decorations --force\n")
    a("# or: code --install-extension /path/to/fsharp-pure-decorations-*.vsix --force\n")
    a("```\n")
    a(
        "Run install steps again in `postAttachCommand` if the `code` CLI is only "
        "available after the editor attaches.\n"
    )

    a("### 2.4 Skeleton `devcontainer.json`\n")
    a(
        "Illustrative only — keep your own base image and features; "
        "merge the fspure-related parts:\n"
    )
    skeleton = {
        "name": "My F# app + fspure",
        "image": "mcr.microsoft.com/devcontainers/dotnet:1-10.0-noble",
        "remoteUser": "vscode",
        "postCreateCommand": "bash .devcontainer/setup-fspure-customer.sh",
        "customizations": {
            "vscode": {
                "extensions": customer_required_ext,
                "settings": {
                    **full_workspace,
                    "dotnet.defaultSolution": "YourSolution.sln",
                    "FSharp.workspacePath": "YourSolution.sln",
                },
            }
        },
    }
    a(json_block(skeleton))
    a(
        "Point `dotnet.defaultSolution` / `FSharp.workspacePath` at **your** solution file. "
        "Implement `setup-fspure-customer.sh` with restore + analyzer mirror + optional VSIX install "
        "as in §2.3.\n"
    )

    a("---\n")
    a("## See also\n")
    a("- [README — consume fspure](README.md#consume-fspure-end-users)\n")
    a("- [FSharp.PureAnalyzer](FSharp.PureAnalyzer/README.md)\n")
    a("- [VS Code extension](vscode-extension/README.md)\n")
    a(
        "- Maintainer publishing: [docs/PUBLISHING.md](docs/PUBLISHING.md)\n"
    )

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
            print(f"ERROR: missing {OUTPUT}; run generate-customer-md.py", file=sys.stderr)
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
