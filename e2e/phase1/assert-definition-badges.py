#!/usr/bin/env python3
"""Phase 1: assert PureAnalyzer definition diagnostics match the baseline.

Maps diagnostic codes the same way the VS Code extension does for badges:
  PURE002 -> impure
  PURE003 -> pure
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path


CODE_TO_BADGE = {
    "PURE002": "impure",
    "PURE003": "pure",
}

FUNC_RE = re.compile(
    r"Function\s+'(?P<name>[^']+)'\s+is\s+(?:not\s+)?transitively\s+pure",
    re.IGNORECASE,
)


def short_name(full: str) -> str:
    return full.rsplit(".", 1)[-1]


def load_expectations(path: Path) -> dict[str, str]:
    data = json.loads(path.read_text(encoding="utf-8"))
    defs = data.get("definitions")
    if not isinstance(defs, dict) or not defs:
        raise SystemExit(f"No definitions in expectations file: {path}")
    out: dict[str, str] = {}
    for k, v in defs.items():
        if v not in ("pure", "impure"):
            raise SystemExit(f"Invalid badge '{v}' for '{k}' (want pure|impure)")
        out[str(k)] = v
    return out


def iter_sarif_results(sarif_path: Path):
    doc = json.loads(sarif_path.read_text(encoding="utf-8"))
    for run in doc.get("runs", []):
        for result in run.get("results", []):
            yield result


def extract_definition_badges(sarif_path: Path) -> dict[str, str]:
    found: dict[str, str] = {}
    for result in iter_sarif_results(sarif_path):
        rule_id = result.get("ruleId") or ""
        badge = CODE_TO_BADGE.get(rule_id)
        if badge is None:
            continue

        message = result.get("message") or {}
        msg = message.get("text") if isinstance(message, dict) else str(message)
        m = FUNC_RE.search(msg or "")
        if not m:
            continue

        name = short_name(m.group("name"))
        if name in found and found[name] == "impure":
            continue
        if badge == "impure" or name not in found:
            found[name] = badge
    return found


def write_baseline(path: Path, actual: dict[str, str]) -> None:
    payload = {
        "$schema_comment": (
            "Baseline definition badges for e2e/customer-fixture/Program.fs. "
            "PURE002 → impure, PURE003 → pure. Regenerate with: "
            "UPDATE_BASELINE=1 bash e2e/phase1/run.sh"
        ),
        "definitions": dict(sorted(actual.items())),
        "notes": [
            "Badges in the editor are driven only by PURE002 (impure) and PURE003 (pure).",
            "PURE001 call-site hints are ignored by fsharp-pure-decorations for badge placement.",
            "This fixture mixes misnamed pure* helpers (impure) with truly pure helpers "
            "(add/isEmpty/myEmpty).",
        ],
    }
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--sarif", required=True, type=Path)
    ap.add_argument("--expectations", required=True, type=Path)
    ap.add_argument(
        "--write-baseline",
        type=Path,
        default=None,
        help="Rewrite expectations JSON from SARIF and exit 0.",
    )
    ap.add_argument(
        "--write-report",
        type=Path,
        default=None,
        help="Write a human-readable comparison report.",
    )
    ap.add_argument(
        "--allow-extra",
        action="store_true",
        help="Do not fail if analyzer reports extra definitions.",
    )
    args = ap.parse_args()

    actual = extract_definition_badges(args.sarif)

    if args.write_baseline is not None:
        write_baseline(args.write_baseline, actual)
        print(f"Wrote baseline with {len(actual)} definitions → {args.write_baseline}")
        return 0

    expected = load_expectations(args.expectations)

    lines: list[str] = []
    lines.append("=== Expected (baseline) ===")
    for name, badge in sorted(expected.items()):
        lines.append(f"  {name:24} {badge}")
    lines.append("=== Actual (analyzer) ===")
    for name, badge in sorted(actual.items()):
        mark = "OK" if expected.get(name) == badge else ("??" if name not in expected else "FAIL")
        lines.append(f"  [{mark:4}] {name:24} {badge}")

    errors: list[str] = []
    for name in sorted(set(expected) - set(actual)):
        errors.append(f"MISSING  {name}: expected '{expected[name]}'")
    for name, want in sorted(expected.items()):
        got = actual.get(name)
        if got is not None and got != want:
            errors.append(f"MISMATCH {name}: expected '{want}', got '{got}'")
    if not args.allow_extra:
        for name in sorted(set(actual) - set(expected)):
            errors.append(f"UNEXPECTED {name}: got '{actual[name]}'")

    report = "\n".join(lines) + "\n"
    print(report, end="")
    if args.write_report is not None:
        args.write_report.parent.mkdir(parents=True, exist_ok=True)
        body = report
        if errors:
            body += "\n=== FAILURES ===\n" + "\n".join(errors) + "\n"
        else:
            body += "\nAll expected pure/impure definition badges matched.\n"
        args.write_report.write_text(body, encoding="utf-8")

    if errors:
        print("\n=== FAILURES ===", file=sys.stderr)
        for e in errors:
            print(e, file=sys.stderr)
        print(
            "\nPhase 1 baseline mismatch. If the new classification is intentional:\n"
            "  UPDATE_BASELINE=1 bash e2e/phase1/run.sh\n"
            "then commit e2e/customer-fixture/expectations.json",
            file=sys.stderr,
        )
        return 1

    print("\nAll expected pure/impure definition badges matched.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
