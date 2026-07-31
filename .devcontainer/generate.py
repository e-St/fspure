#!/usr/bin/env python3
"""Generate flavour-specific devcontainer.json files from shared fragments.

Dev Containers do not support config inheritance. This repo keeps shared
fragments plus per-flavour overlays under .devcontainer/fragments/flavours/
and merges them into the three committed outputs (IDE, PureAnalyzer build, e2e).

Usage (from repo root or this directory):
  python3 .devcontainer/generate.py          # write outputs
  python3 .devcontainer/generate.py --check  # exit 1 if outputs are stale
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any

HERE = Path(__file__).resolve().parent
FRAGMENTS = HERE / "fragments"
FLAVOURS = FRAGMENTS / "flavours.json"

BANNER = (
    "// GENERATED FILE — do not edit by hand.\n"
    "// Source: .devcontainer/fragments/  |  Regenerate: python3 .devcontainer/generate.py\n"
)


def deep_merge(base: Any, overlay: Any) -> Any:
    """Recursive merge. Overlay wins on scalars/lists; null deletes a key."""
    if overlay is None:
        return None
    if not isinstance(base, dict) or not isinstance(overlay, dict):
        return overlay
    out: dict[str, Any] = dict(base)
    for key, value in overlay.items():
        if value is None:
            out.pop(key, None)
        elif key in out and isinstance(out[key], dict) and isinstance(value, dict):
            out[key] = deep_merge(out[key], value)
        else:
            out[key] = value
    return out


def load_json(path: Path) -> Any:
    with path.open(encoding="utf-8") as f:
        return json.load(f)


def strip_jsonc_banner(text: str) -> str:
    """Drop leading // comment lines so we can compare/parse generated files."""
    lines = text.splitlines()
    i = 0
    while i < len(lines) and (
        lines[i].strip().startswith("//") or lines[i].strip() == ""
    ):
        i += 1
    return "\n".join(lines[i:]) + ("\n" if text.endswith("\n") else "")


def render(doc: dict[str, Any]) -> str:
    body = json.dumps(doc, indent=2, ensure_ascii=False) + "\n"
    return BANNER + body


def build_flavor(fragment_names: list[str]) -> dict[str, Any]:
    doc: dict[str, Any] = {}
    for name in fragment_names:
        path = FRAGMENTS / name
        if not path.is_file():
            raise FileNotFoundError(f"missing fragment: {path}")
        doc = deep_merge(doc, load_json(path))
    if not isinstance(doc, dict):
        raise TypeError("merged document must be an object")
    # Drop keys explicitly nulled away at the top level.
    return {k: v for k, v in doc.items() if v is not None}


def load_flavour_table(doc: dict[str, Any]) -> dict[str, Any]:
    """Accept British (flavours) or American (flavors) top-level keys."""
    if "flavours" in doc:
        return doc["flavours"]
    if "flavors" in doc:
        return doc["flavors"]
    raise KeyError("flavours.json must define 'flavours' (or 'flavors')")


def generate_all() -> dict[str, Path]:
    table = load_flavour_table(load_json(FLAVOURS))
    written: dict[str, Path] = {}
    for flavour, cfg in table.items():
        doc = build_flavor(cfg["fragments"])
        out_path = (HERE / cfg["output"]).resolve()
        out_path.parent.mkdir(parents=True, exist_ok=True)
        text = render(doc)
        out_path.write_text(text, encoding="utf-8")
        written[flavour] = out_path
    return written


def check_all() -> list[str]:
    """Return list of stale flavour names (empty if all match)."""
    table = load_flavour_table(load_json(FLAVOURS))
    stale: list[str] = []
    for flavour, cfg in table.items():
        doc = build_flavor(cfg["fragments"])
        out_path = (HERE / cfg["output"]).resolve()
        if not out_path.is_file():
            stale.append(f"{flavour} (missing {out_path})")
            continue
        actual = out_path.read_text(encoding="utf-8")
        # Compare normalized JSON (ignore banner formatting drift).
        try:
            actual_doc = json.loads(strip_jsonc_banner(actual))
        except json.JSONDecodeError:
            stale.append(f"{flavour} (invalid JSON in {out_path})")
            continue
        if actual_doc != doc or not actual.startswith("// GENERATED"):
            stale.append(flavour)
    return stale


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--check",
        action="store_true",
        help="exit 1 if generated outputs are missing or out of date",
    )
    args = parser.parse_args(argv)

    if args.check:
        stale = check_all()
        if stale:
            print(
                "Generated devcontainer.json files are out of date:\n  - "
                + "\n  - ".join(stale)
                + "\n\nRun: python3 .devcontainer/generate.py",
                file=sys.stderr,
            )
            return 1
        print("OK: all generated devcontainer.json files are up to date.")
        return 0

    written = generate_all()
    for flavour, path in written.items():
        try:
            rel = path.relative_to(HERE.parent)
        except ValueError:
            rel = path
        print(f"  wrote {flavour:5} → {rel}")
    print("Done.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
