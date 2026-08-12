#!/usr/bin/env bash
# After an official release, pin all in-repo consumers to lastOfficial versions.
set -euo pipefail

# shellcheck source=lib.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"
require_cmd python3

cd "$ROOT"

python3 - <<'PY'
import json
import pathlib
import re

root = pathlib.Path(".")
m = json.loads((root / "releases" / "manifest.json").read_text())
last = m["lastOfficial"]
analyzer = last["FSharp.PureAnalyzer"]
collector = last["fspure-collector"]
extension = last["fsharp-pure-decorations"]


def sub1(path: pathlib.Path, pattern: str, repl: str) -> None:
    if not path.exists():
        print("skip missing", path)
        return
    text = path.read_text()
    new, n = re.subn(pattern, repl, text, count=1)
    if n:
        path.write_text(new)
        print("updated", path)
    else:
        # try global replace for multi-occurrence version strings
        new2, n2 = re.subn(pattern, repl, text)
        if n2:
            path.write_text(new2)
            print("updated", path, f"({n2} places)")
        else:
            print("unchanged", path)


sub1(
    root / "FSharp.PureAnalyzer" / "FSharp.PureAnalyzer.fsproj",
    r"(<Version>)[^<]+(</Version>)",
    rf"\g<1>{analyzer}\g<2>",
)
sub1(
    root / "fspure-collector" / "fspure-collector.fsproj",
    r"(<Version>)[^<]+(</Version>)",
    rf"\g<1>{collector}\g<2>",
)
sub1(
    root / "vscode-extension" / "package.json",
    r'("version"\s*:\s*")[^"]+(")',
    rf"\g<1>{extension}\g<2>",
)
sub1(
    root / "samples" / "fspure-ready-lib" / "Directory.Packages.props",
    r"(<FspureAnalyzerVersion Condition=\"'\$\(FspureAnalyzerVersion\)' == ''\">)[^<]+(</FspureAnalyzerVersion>)",
    rf"\g<1>{analyzer}\g<2>",
)
sub1(
    root / "src" / "scripts" / "integrations" / "fstarter" / "versions.env",
    r"(FSPURE_ANALYZER_VERSION=).*",
    rf"\g<1>{analyzer}",
)
# gh skill install --pin: next official tag includes the skill (v0.4.0 does not).
sub1(
    root / "src" / "scripts" / "integrations" / "fstarter" / "versions.env",
    r"(FSPURE_SKILL_REF=).*",
    rf"\g<1>v{analyzer}",
)

readme = root / "samples" / "fspure-ready-lib" / "README.md"
if readme.exists():
    t = readme.read_text()
    t2 = re.sub(
        r'(PackageReference Include="FSharp\.PureAnalyzer" Version=")[^"]+(")',
        rf"\g<1>{analyzer}\g<2>",
        t,
    )
    t2 = re.sub(r"\*\*0\.\d+\.\d+\+\*\* \(Phase 3\)", f"**{analyzer}+** (Phase 3)", t2)
    t2 = re.sub(r"Use \*\*0\.\d+\.\d+\+\*\*", f"Use **{analyzer}+**", t2)
    if t2 != t:
        readme.write_text(t2)
        print("updated", readme)

root_readme = root / "README.md"
if root_readme.exists():
    t = root_readme.read_text()
    t2 = re.sub(
        r"fsharp-pure-decorations-\d+\.\d+\.\d+\.vsix",
        f"fsharp-pure-decorations-{extension}.vsix",
        t,
    )
    if t2 != t:
        root_readme.write_text(t2)
        print("updated", root_readme)

resolve = root / "samples" / "fspure-ready-lib" / "scripts" / "resolve-fspure-analyzer-version.sh"
if resolve.exists():
    t = resolve.read_text()
    t2 = re.sub(
        r'FALLBACK="\$\{FSPURE_ANALYZER_FALLBACK_VERSION:-\d+\.\d+\.\d+\}"',
        f'FALLBACK="${{FSPURE_ANALYZER_FALLBACK_VERSION:-{analyzer}}}"',
        t,
    )
    if t2 != t:
        resolve.write_text(t2)
        print("updated", resolve)

print("Pins applied:", json.dumps(last, indent=2))
PY

echo "apply-version-pins done"
