#!/usr/bin/env python3
"""Build an fstarter sync commit/PR title and body from the staged diff.

Run from the fstarter checkout after `git add -A` (HEAD is still fstarter main).

Prints TITLE, a blank line, then the PR body.
Exit 2 if the only change is sync metadata (caller should skip the PR).
"""
from __future__ import annotations

import argparse
import re
import subprocess
import sys
from pathlib import Path

METADATA_PATHS = {".fspure-sync-source"}

FILE_NOTES = {
    ".devcontainer/setup-fspure.sh": "Codespace install script (baked analyzer, decorations VSIX unpack, CLI, Copilot skill)",
    ".devcontainer/devcontainer.json": "postCreate-only Ionide / decorations / LineLens settings",
    "Directory.Build.props": "strict F# compiler rules from the fspure pack",
    ".gitignore": "ignore Ionide `analyzers/` drop",
}

PIN_KEYS = (
    ("FSPURE_ANALYZER_VERSION", "analyzer", "FSharp.PureAnalyzer"),
    ("FSPURE_SKILL_REF", "skill", "Skill (`gh skill --pin`)"),
    ("FSPURE_CLI_RELEASE", "CLI", "CLI release"),
)


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


def pin_changes(old: dict[str, str], new: dict[str, str]) -> list[tuple[str, str, str, str]]:
    rows = []
    for key, short, label in PIN_KEYS:
        a, b = old.get(key, ""), new.get(key, "")
        if a != b:
            rows.append((key, short, a, b))
    return rows


def file_note(path: str, old_pins: dict[str, str], new_pins: dict[str, str]) -> str:
    if path == ".devcontainer/fspure-versions.env":
        bits = [f"{short} `{a or '—'}` → `{b or '—'}`" for _, short, a, b in pin_changes(old_pins, new_pins)]
        return "pins: " + (", ".join(bits) if bits else "comments / formatting only")
    return FILE_NOTES.get(path, "fspure integration pack")


def title_from_subject(subject: str) -> str:
    s = re.sub(r"\s+", " ", subject).strip()
    s = re.sub(r"^(chore|feat|fix|docs)(\([^)]+\))?:\s*", "", s, flags=re.I)
    if not s:
        return ""
    if not s.lower().startswith("fspure"):
        s = f"fspure: {s[0].lower() + s[1:] if s[0].isupper() and not s.startswith('F#') else s}"
    if len(s) > 72:
        s = s[:69].rstrip() + "..."
    return s


def title_for(
    old_pins: dict[str, str],
    new_pins: dict[str, str],
    paths: list[str],
    subject: str,
) -> str:
    phrases: list[str] = []
    for _, short, a, b in pin_changes(old_pins, new_pins):
        if a and b:
            phrases.append(f"pin {short} {a} → {b}")
        elif b:
            phrases.append(f"pin {short} {b}")
        else:
            phrases.append(f"drop {short} pin")

    path_set = set(paths) - METADATA_PATHS
    if not phrases:
        if ".devcontainer/setup-fspure.sh" in path_set:
            phrases.append("update Codespace setup")
        if ".devcontainer/devcontainer.json" in path_set:
            phrases.append("update Codespace settings")
        if "Directory.Build.props" in path_set:
            phrases.append("update compiler rules")
        if ".devcontainer/fspure-versions.env" in path_set:
            phrases.append("update version pins file")

    if phrases:
        title = "fspure: " + " and ".join(phrases[:2])
        if len(phrases) > 2:
            title += f" (+{len(phrases) - 2} more)"
        if len(title) > 90:
            title = title[:87] + "..."
        return title

    from_subject = title_from_subject(subject)
    return from_subject or "fspure: update integration pack"


def lead_sentence(
    old_pins: dict[str, str],
    new_pins: dict[str, str],
    paths: list[str],
) -> str:
    changed = pin_changes(old_pins, new_pins)
    if len(changed) == 1:
        _, short, a, b = changed[0]
        if short == "skill":
            return (
                f"Codespaces from this template will install the Copilot skill at `{b}` "
                f"(`gh skill install --pin`) instead of `{a or '—'}`. "
                "A fork keeps that pin until it merges a later fspure update."
            )
        if short == "analyzer":
            return (
                f"Codespaces from this template will restore **FSharp.PureAnalyzer** `{b}` "
                f"from nuget.org (was `{a or '—'}`)."
            )
        if short == "CLI":
            return f"Codespaces will install the standalone `fspure` CLI from GitHub Release `{b}` (was `{a or '—'}`)."
    if changed:
        bits = [f"{short} `{a or '—'}` → `{b}`" for _, short, a, b in changed]
        return "Updates fspure pins in the template: " + "; ".join(bits) + "."
    if ".devcontainer/setup-fspure.sh" in paths:
        return (
            "Updates how the Codespace installs fspure (baked analyzer, decorations VSIX unpack, CLI, Copilot skill). "
            "Pins are unchanged."
        )
    if ".devcontainer/devcontainer.json" in paths:
        return "Updates fspure Ionide / decorations settings in the Codespace (postCreate-only). Pins are unchanged."
    if "Directory.Build.props" in paths:
        return "Updates the fspure compiler rules copied into fstarter. Pins are unchanged."
    return "Updates the fspure integration pack in this template."


def why_line(event: str, subject: str, ref_name: str) -> str:
    subject = re.sub(r"\s+", " ", subject).strip()
    if event == "release":
        base = f"Opened because fspure published GitHub Release `{ref_name}`."
    elif event == "workflow_dispatch":
        base = "Opened from a manual **PR fspure updates to fstarter** run."
    elif event == "push":
        base = "Opened by a push to `e-St/fspure` `main` that changes the fstarter pack."
    else:
        base = f"Opened by `{event}`."
    if subject:
        return f"{base}\n\nFrom fspure: *{subject}*"
    return base


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--fstarter", default=".")
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
    status_rows: list[tuple[str, str]] = []
    for line in name_status.splitlines():
        if not line.strip():
            continue
        status, path = line.split("\t", 1)
        status_rows.append((status, path))
    paths = [path for _, path in status_rows]

    if paths and set(paths) <= METADATA_PATHS:
        print("SKIP: only .fspure-sync-source changed", file=sys.stderr)
        return 2

    title = title_for(old_pins, new_pins, paths, args.subject)
    sha = args.fspure_sha
    short = sha[:7] if sha else "local"
    repo = args.fspure_repo
    server = args.server_url.rstrip("/")

    changed = pin_changes(old_pins, new_pins)
    pin_section: list[str]
    if changed:
        pin_section = [
            "### Pins that change",
            "",
            "| | fstarter `main` | This PR |",
            "|--|--|--|",
        ]
        for key, _short, a, b in changed:
            label = next(lbl for k, _, lbl in PIN_KEYS if k == key)
            pin_section.append(f"| **{label}** | `{a or '—'}` | `{b or '—'}` |")
        unchanged = [
            f"{lbl} stays `{new_pins.get(k) or '—'}`"
            for k, _s, lbl in PIN_KEYS
            if k not in {c[0] for c in changed} and new_pins.get(k)
        ]
        if unchanged:
            pin_section += ["", ", ".join(unchanged) + "."]
    else:
        pin_section = [
            "### Pins",
            "",
            "Unchanged: "
            + ", ".join(
                f"{lbl} `{new_pins.get(k) or '—'}`" for k, _s, lbl in PIN_KEYS if new_pins.get(k)
            )
            + ".",
        ]

    file_lines = []
    for status, path in status_rows:
        if path in METADATA_PATHS:
            continue
        file_lines.append(f"- `{path}` — {file_note(path, old_pins, new_pins)}")
    if not file_lines:
        file_lines = ["- _(pack files already match fstarter `main`)_"]

    source = f"{server}/{repo}/commit/{sha}" if sha else ""
    run_link = f"{server}/{repo}/actions/runs/{args.run_id}" if args.run_id else ""

    lines = [
        f"## {title}",
        "",
        lead_sentence(old_pins, new_pins, paths),
        "",
        why_line(args.event, args.subject, args.ref_name),
        "",
        *pin_section,
        "",
        "### Files",
        "",
        *file_lines,
        "",
        "### Source",
        "",
    ]
    if sha:
        lines.append(f"- fspure: [`{short}`]({source})")
    if run_link:
        lines.append(f"- workflow: [run]({run_link})")
    lines += [
        "",
        "Does **not** overwrite `Dockerfile`, `newf.sh`, or other fstarter-owned files.",
        "",
        "After merge: rebuild the Codespace / container so `setup-fspure.sh` applies this.",
        "",
    ]
    body = "\n".join(lines)

    print(title)
    print()
    print(body.rstrip() + "\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
