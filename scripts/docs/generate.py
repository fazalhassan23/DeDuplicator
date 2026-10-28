"""Generate deterministic references from application sources, without executing app code."""
from __future__ import annotations

import argparse
import os
import re
import sqlite3
import xml.etree.ElementTree as ET
from pathlib import Path
from urllib.parse import quote, unquote, urlsplit, urlunsplit

ROOT = Path(__file__).resolve().parents[2]
SEMVER = re.compile(r"(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*))*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?")


def relocate_links(text: str, source: Path, destination: Path) -> str:
    """Keep fragment links pointing at the same target after assembly or archival."""
    def replace(match):
        target = match[1]
        parsed = urlsplit(target)
        if parsed.scheme or parsed.netloc or not parsed.path:
            return match[0]
        absolute = (source / unquote(parsed.path)).resolve()
        relative = os.path.relpath(absolute, destination).replace("\\", "/")
        url = urlunsplit(("", "", quote(relative, safe="/.-_"), parsed.query, parsed.fragment))
        return "](" + url + ")"
    return re.sub(r"\]\(([^\s)]+)\)", replace, text)


def version(root: Path = ROOT) -> str:
    value = ET.parse(root / "Version.props").findtext("./PropertyGroup/Version", "")
    if not SEMVER.fullmatch(value):
        raise ValueError("Version.props must contain a valid Semantic Version in PropertyGroup/Version")
    return value


def schema_sql(root: Path = ROOT) -> str:
    source = (root / "src/DeDuplicator.Infrastructure/ScanStore.cs").read_text(encoding="utf-8")
    matches = re.findall(r'cmd\.CommandText\s*=\s*"""\s*(.*?)\s*""";', source, re.S)
    if len(matches) != 1 or "CREATE TABLE" not in matches[0]:
        raise ValueError("Cannot identify authoritative ScanStore initializer; update the schema extractor")
    return matches[0]


def schema_reference(root: Path = ROOT) -> str:
    lines = ["# Generated SQLite structure", "", "Generated from the initializer in `src/DeDuplicator.Infrastructure/ScanStore.cs`.",
             "Do not edit this reference; run `npm run docs:generate`. Explanations and lifecycle rules are in [schema](../schema.md).", ""]
    with sqlite3.connect(":memory:") as db:
        db.executescript(schema_sql(root))
        tables = db.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name").fetchall()
        for (table,) in tables:
            lines += [f"## {table}", "", "| Column | SQLite type | NOT NULL clause | Default | PK position |", "| --- | --- | --- | --- | --- |"]
            for _, name, kind, required, default, pk in db.execute(f'PRAGMA table_info("{table}")'):
                lines.append(f"| `{name}` | `{kind}` | {'yes' if required else 'no'} | {default if default is not None else 'none'} | {pk} |")
            lines += ["", "### Indexes", ""]
            for _, name, unique, origin, _ in sorted(db.execute(f'PRAGMA index_list("{table}")'), key=lambda row: row[1]):
                columns = ", ".join(row[2] for row in db.execute(f'PRAGMA index_info("{name}")'))
                lines.append(f"- `{name}`: ({columns}); unique: {'yes' if unique else 'no'}; origin: `{origin}`.")
            if not db.execute(f'PRAGMA index_list("{table}")').fetchall():
                lines.append("No indexes beyond the INTEGER PRIMARY KEY rowid alias, when present.")
            lines += ["", "Foreign keys: " + ("present" if db.execute(f'PRAGMA foreign_key_list("{table}")').fetchall() else "none") + ".", ""]
        lines += ["## SQLite constraint interpretation", "", "`INTEGER PRIMARY KEY` aliases rowid and generates an integer when NULL is inserted. Other PRIMARY KEY columns are ordinary TEXT rowid-table keys: SQLite can accept NULL despite the key declaration. There are no explicit NOT NULL, DEFAULT, CHECK or FOREIGN KEY clauses in this schema. Application callers supply values; that is not a database constraint.", ""]
    return "\n".join(lines)


def entity_diagram(root: Path = ROOT) -> str:
    lines = ["# Generated persistent entities", "", "Generated from the same SQLite initializer as the [structural reference](schema-reference.md).", "Relationships shown here are logical application relationships, not enforced foreign keys.", "", "```mermaid", "erDiagram"]
    with sqlite3.connect(":memory:") as db:
        db.executescript(schema_sql(root))
        for (table,) in db.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name").fetchall():
            lines.append(f"    {table} {{")
            for _, name, kind, _, _, pk in db.execute(f'PRAGMA table_info("{table}")'):
                lines.append(f"        {kind} {name}" + (" PK" if pk else ""))
            lines.append("    }")
    lines += ['    scans ||--o{ files : "logical scan id"', '    scans ||--o{ issues : "logical scan id"', "```", "", "`files.group_id` is a provisional representative file-row ID, without a foreign key or group table. `journal.plan` is an action-plan UUID without a persisted plan table. Journal snapshots intentionally survive independently of scan rows.", ""]
    return "\n".join(lines)


def references(root: Path = ROOT) -> dict[str, str]:
    result = {
        "docs/generated/schema-reference.md": schema_reference(root),
        "docs/generated/entities.md": entity_diagram(root),
        "docs/generated/version.md": f"# Generated version reference\n\n| Concept | Current source and value |\n| --- | --- |\n| Application release | `Version.props`: `{version(root)}` |\n| Database schema | Unversioned initializer; SQLite `user_version` remains `0` |\n| Profile and export formats | Unversioned JSON/CSV; no format-version field |\n| CI build | Workflow run ID and Git commit; not the application release version |\n\nRegenerate with `npm run docs:generate`. See [versioning](../versioning-and-releases.md) for compatibility and release policy.\n"
    }
    changelog = root / "CHANGELOG.md"
    if changelog.exists():
        text = changelog.read_text(encoding="utf-8")
        sections = []
        for category in ("added", "changed", "fixed", "deprecated", "removed", "security"):
            entries = [p.read_text(encoding="utf-8").strip().replace("\n", " ") for p in sorted((root / ".changes").glob(f"*.{category}.md"))]
            if entries:
                # Relative links originate in fragments, one directory beneath repository root.
                entries = [relocate_links(entry, root / ".changes", root) for entry in entries]
                sections += [f"### {category.title()}", ""] + ["- " + entry for entry in entries] + [""]
        body = "\n".join(sections).strip() if sections else "No unreleased entries."
        pattern = r"(?<=<!-- unreleased:start -->).*?(?=<!-- unreleased:end -->)"
        if len(re.findall(pattern, text, re.S)) != 1:
            raise ValueError("CHANGELOG.md needs exactly one unreleased:start/end region")
        result["CHANGELOG.md"] = re.sub(pattern, "\n\n" + body + "\n\n", text, flags=re.S)
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true", help="Regenerate committed references")
    args = parser.parse_args()
    failures = []
    for relative, expected in references().items():
        path = ROOT / relative
        if args.write:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(expected, encoding="utf-8", newline="\n")
            print(f"Generated {relative}")
        elif not path.exists() or path.read_text(encoding="utf-8") != expected:
            failures.append(relative)
    if failures:
        print("Generated documentation drift: " + ", ".join(failures) + ". Run npm run docs:generate.")
    return bool(failures)


if __name__ == "__main__":
    raise SystemExit(main())
