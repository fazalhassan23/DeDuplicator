"""Assemble reviewed fragments into a local changelog; never tag, push or publish."""
from __future__ import annotations

import argparse
import re
from datetime import date
from pathlib import Path

from check import fragment_errors
from generate import ROOT, version, relocate_links

CATEGORIES = ["added", "changed", "fixed", "deprecated", "removed", "security"]


def assemble(root: Path, release: str, released_on: str) -> tuple[str, list[Path]]:
    if release != version(root):
        raise ValueError("Release version must match Version.props; set it during release preparation first")
    if not re.fullmatch(r"\d{4}-\d{2}-\d{2}", released_on):
        raise ValueError("Release date must be YYYY-MM-DD")
    date.fromisoformat(released_on)
    path = root / "CHANGELOG.md"
    existing = path.read_text(encoding="utf-8")
    heading = f"## [{release}] - {released_on}"
    if f"## [{release}]" in existing:
        raise ValueError("That release already exists; published entries and tags are immutable")
    fragments = sorted(p for p in (root / ".changes").glob("*.md") if p.name != "README.md")
    if not fragments:
        raise ValueError("No fragments to assemble")
    for fragment in fragments:
        failures = fragment_errors(fragment)
        if failures:
            raise ValueError("; ".join(failures))
    sections = [heading, ""]
    for category in CATEGORIES:
        entries = [p.read_text(encoding="utf-8").strip() for p in fragments if p.name.endswith(f".{category}.md")]
        if entries:
            sections += [f"### {category.title()}", ""]
            sections += ["- " + relocate_links(entry.replace("\n", " "), root / ".changes", root) for entry in entries]
            sections += [""]
    marker = "<!-- changelog-fragments -->"
    if existing.count(marker) != 1:
        raise ValueError("CHANGELOG.md needs exactly one changelog-fragments marker")
    existing = re.sub(r"(?<=<!-- unreleased:start -->).*?(?=<!-- unreleased:end -->)", "\n\nNo unreleased entries.\n\n", existing, flags=re.S)
    return existing.replace(marker, marker + "\n\n" + "\n".join(sections).rstrip()), fragments


def write_release(root: Path, release: str, released_on: str) -> None:
    text, fragments = assemble(root, release, released_on)
    archive = root / ".changes/archive" / release
    if any((archive / fragment.name).exists() for fragment in fragments):
        raise ValueError("Archived fragment collision; no changelog was changed")
    archive.mkdir(parents=True, exist_ok=True)
    (root / "CHANGELOG.md").write_text(text, encoding="utf-8", newline="\n")
    for fragment in fragments:
        archived = relocate_links(fragment.read_text(encoding="utf-8"), fragment.parent, archive)
        (archive / fragment.name).write_text(archived, encoding="utf-8", newline="\n")
        fragment.unlink()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version", required=True)
    parser.add_argument("--date", required=True, help="Explicit release date, YYYY-MM-DD")
    parser.add_argument("--write", action="store_true", help="Write changelog and archive consumed fragments locally")
    args = parser.parse_args()
    text, fragments = assemble(ROOT, args.version, args.date)
    if not args.write:
        print(text)
        return
    # Inputs are fully validated before any mutation; release preparation remains a reviewed Git change.
    write_release(ROOT, args.version, args.date)
    print(f"Assembled {args.version}; review and commit CHANGELOG.md and .changes/archive. No tag or release was created.")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError) as error:
        raise SystemExit(str(error)) from error
