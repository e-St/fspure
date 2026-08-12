#!/usr/bin/env python3
"""Build an fstarter sync commit/PR title and body from the staged diff.

Run from the fstarter checkout after `git add -A` (HEAD is still fstarter main).
Prints TITLE on the first line, then a blank line, then the PR body.
"""
from __future__ import annotations

import argparse
import re
import subprocess
from pathlib import Path


def run(args: list[str], cwd: Path | None = None) -> str:
    return subprocess.check_output(args, cwd=cwd, text=True).rstrip("\n")


def parse_env(text: str) -> dict[str, str]:
    out: dict[str, str] = {}
    for line in text.splitlines():
        line = line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        k, _, v = line.partition("=")
        out[k.strip()] = v.strip()
    return out


def show_head(path: str, cwd: Path) -> str:
    try:
        return run(["git", "show", f"HEAD:{path}"], cwd=cwd)
    except subprocess.CalledProcessError:
        return ""


def pin_cell(value: str) -> str:
    return f"`{value}`" if value else "—"


def file_note(path: str, old_pins: dict[str, str], new_pins: dict[str, str]) -> str:
    notes = {
        ".devcontainer/setup-fspure.sh": "Codespace install script (analyzer drop, CLI, Copilot skill)",
        ".devcontainer/devcontainer.json": "Ionide / decorations / LineLens settings",
        "Directory.Build.props": "strict F# compiler rules from the fspure pack",
        ".fspure-sync-source": "sync metadata (source SHA / workflow run)",
        ".gitignore": "ignore Ionide `analyzers/` drop",
    }
    if path == ".devcontainer/fspure-versions.env":
        bits = []
        for key, label in (
            ("FSPURE_ANALYZER_VERSION", "analyzer"),
            ("FSPURE_SKILL_REF", "skill"),
            ("FSPURE_CLI_RELEASE", "CLI"),
        ):
            a, b = old_pins.get(key, ""), new_pins.get(key, "")
            if a != b:
                bits.append(f"{label} `{a or '—'}` → `{b or '—'}`")
        return "pins: " + (", ".join(bits) if bits else "comments / formatting only")
    return notes.get(path, "fspure integration pack")


def title_for(
    old_pins: dict[str, str],
    new_pins: dict[str, str],
    paths: list[str],
) -> str:
    phrases: list[str] = []
    pairs = (
        ("FSPURE_ANALYZER_VERSION", "analyzer"),
        ("FSPURE_SKILL_REF", "skill"),
        ("FSPURE_CLI_RELEASE", "CLI"),
    )
    for key, label in pairs:
        a, b = old_pins.get(key, ""), new_pins.get(key, "")
        if a != b:
            if a and b:
                phrases.append(f"pin {label} {a} → {b}")
            elif b:
                phrases.append(f"pin {label} {b}")
            else:
                phrases.append(f"drop {label} pin")

    path_set = set(paths)
    if not phrases:
        if ".devcontainer/setup-fspure.sh" in path_set:
            phrases.append("update Codespace setup")
        if ".devcontainer/devcontainer.json" in path_set:
            phrases.append("update Codespace settings")
        if "Directory.Build.props" in path_set:
            phrases.append("update compiler rules")
    if not phrases:
        phrases.append("refresh integration pack")

    title = "fspure: " + " and ".join(phrases[:2])
    if len(phrases) > 2:
        title += f" (+{len(phrases) - 2} more)"
    if len(title) > 90:
        title = title[:87] + "..."
    return title


def why_line(event: str, subject: str, ref_name: str) -> str:
    subject = re.sub(r"\s+", " ", subject).strip()
    if event == "release":
        base = f"Triggered by publishing GitHub Release `{ref_name}`."
    elif event == "workflow_dispatch":
        base = "Triggered by a manual **PR fspure updates to fstarter** run."
    elif event == "push":
        base = "Triggered by a push to `e-St/fspure` `main`."
    else:
        base = f"Triggered by `{event}`."
    if subject:
        return f"{base} Head commit: *{subject}*"
    return base


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--fstarter", default=".", help="fstarter checkout (staged diff)")
    p.add_argument("--fspure-sha", default="")
    p.add_argument("--fspure-repo", default="e-St/fspure")
    p.add_argument("--server-url", default="https://github.com")
    p.add_argument("--run-id", default="")
    p.add_argument("--event", default="")
    p.add_argument("--subject", default="")
    p.add_argument("--ref-name", default="")
    args = p.parse_args()

    cwd = Path(args.fstarter).resolve()
    new_pins = parse_env((cwd / ".devcontainer" / "fspure-versions.env").read_text())
    old_pins = parse_env(show_head(".devcontainer/fspure-versions.env", cwd))

    name_status = run(["git", "diff", "--cached", "--name-status"], cwd=cwd)
    paths: list[str] = []
    for line in name_status.splitlines():
        if not line.strip():
            continue
        parts = line.split("\t")
        paths.append(parts[-1])

    title = title_for(old_pins, new_pins, paths)
    sha = args.fspure_sha
    short = sha[:7] if sha else "local"
    repo = args.fspure_repo
    server = args.server_url.rstrip("/")

    pin_rows = [
        (
            "FSharp.PureAnalyzer",
            old_pins.get("FSPURE_ANALYZER_VERSION", ""),
            new_pins.get("FSPURE_ANALYZER_VERSION", ""),
        ),
        (
            "Skill (`gh skill --pin`)",
            old_pins.get("FSPURE_SKILL_REF", ""),
            new_pins.get("FSPURE_SKILL_REF", ""),
        ),
        (
            "CLI release",
            old_pins.get("FSPURE_CLI_RELEASE", ""),
            new_pins.get("FSPURE_CLI_RELEASE", ""),
        ),
    ]

    pin_table = [
        "| | fstarter `main` | This PR |",
        "|--|--|--|",
    ]
    for label, old, new in pin_rows:
        mark = " ← changed" if old != new else ""
        pin_table.append(
            f"| **{label}** | {pin_cell(old)} | {pin_cell(new)}{mark} |"
        )

    file_lines = []
    for line in name_status.splitlines():
        if not line.strip():
            continue
        status, path = line.split("\t", 1)
        file_lines.append(f"- `{path}` ({status}) — {file_note(path, old_pins, new_pins)}")
    if not file_lines:
        file_lines = ["- _(no path list — see git diff)_"]

    why = why_line(args.event, args.subject, args.ref_name)
    source = f"{server}/{repo}/commit/{sha}" if sha else "(local)"
    run_link = (
        f"{server}/{repo}/actions/runs/{args.run_id}" if args.run_id else ""
    )

    lines = [
        f"## {title}",
        "",
        why,
        "",
        "A fork of fstarter keeps these pins until it merges a later fspure update.",
        "",
        "### Pins",
        "",
        *pin_table,
        "",
        "### What actually changed",
        "",
        *file_lines,
        "",
        "### Source",
        "",
        f"- fspure commit: [`{short}`]({source})" if sha else f"- fspure commit: `{short}`",
    ]
    if run_link:
        lines.append(f"- workflow run: [actions]({run_link})")
    lines += [
        "",
        "Does **not** overwrite `Dockerfile`, `newf.sh`, or other fstarter-owned files.",
        "",
        "After merge: rebuild the Codespace / container so `setup-fspure.sh` installs these pins.",
        "",
    ]
    body = "\n".join(lines)

    print(title)
    print()
    print(body.rstrip() + "\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
