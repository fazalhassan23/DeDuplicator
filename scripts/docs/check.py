"""Local/CI documentation gate. File mappings identify obligations, not semantic truth."""
from __future__ import annotations

import argparse
import fnmatch
import json
import os
import re
import subprocess
import sys
import tempfile
from pathlib import Path
from urllib.parse import unquote, urlsplit

from generate import ROOT, references

EXCLUDED = {".git", ".tools", "node_modules", "artifacts", "bin", "obj", "__pycache__", "benchmark-data"}
CATEGORIES = {"added", "changed", "fixed", "deprecated", "removed", "security"}


def run(command: list[str], root: Path = ROOT) -> None:
    subprocess.run(command, cwd=root, check=True)


def markdown_files(root: Path) -> list[Path]:
    result = []
    for directory, children, files in os.walk(root):
        children[:] = sorted(child for child in children if child not in EXCLUDED and not (Path(directory) / child).is_symlink())
        result.extend(Path(directory) / name for name in sorted(files) if name.endswith(".md"))
    return result


def prose(text: str) -> str:
    return re.sub(r"^```[^\n]*\n.*?^```\s*$", "", text, flags=re.M | re.S)


def anchors(text: str) -> set[str]:
    result: set[str] = set()
    counts: dict[str, int] = {}
    for heading in re.findall(r"^#{1,6}\s+(.+?)(?:\s+#+)?$", prose(text), re.M):
        slug = re.sub(r"[^\w\- ]", "", heading.replace("`", "").lower(), flags=re.UNICODE).replace(" ", "-")
        number = counts.get(slug, 0)
        counts[slug] = number + 1
        result.add(slug if number == 0 else f"{slug}-{number}")
    result.update(re.findall(r'<a\s+(?:id|name)=["\']([^"\']+)', text))
    return result


def link_errors(path: Path, root: Path) -> list[str]:
    errors = []
    text = prose(path.read_text(encoding="utf-8"))
    targets = re.findall(r"!?\[[^\]]*\]\((<[^>]+>|[^\s)]+)(?:\s+[\"'][^\n]*[\"'])?\)", text)
    targets += re.findall(r"^\s*\[[^\]]+\]:\s*(<[^>]+>|\S+)", text, re.M)
    for raw in targets:
        target = raw.removeprefix("<").removesuffix(">")
        url = urlsplit(target)
        if url.scheme in {"https", "http", "mailto"}:
            if url.scheme != "mailto" and not url.netloc:
                errors.append(f"{path.relative_to(root)}: malformed external URL {target}")
            continue  # Remote availability is deliberately not probed from untrusted PR content.
        if url.scheme or target.startswith("/"):
            errors.append(f"{path.relative_to(root)}: use a repository-relative link: {target}")
            continue
        destination = (path.parent / unquote(url.path)).resolve() if url.path else path
        if not destination.is_relative_to(root.resolve()) or not destination.exists():
            errors.append(f"{path.relative_to(root)}: broken/outside-repository link {target}")
        elif url.fragment and destination.suffix == ".md" and unquote(url.fragment) not in anchors(destination.read_text(encoding="utf-8")):
            errors.append(f"{path.relative_to(root)}: missing anchor in {target}")
    # Reference-style uses must have a definition; image references are included.
    definitions = {x.casefold() for x in re.findall(r"^\s*\[([^\]]+)\]:", text, re.M)}
    for label, reference in re.findall(r"!?\[([^\]]+)\]\[([^\]]*)\]", text):
        if (reference or label).casefold() not in definitions:
            errors.append(f"{path.relative_to(root)}: undefined link reference {reference or label}")
    return errors


def meaningful(value: object) -> bool:
    if not isinstance(value, str) or len(value.strip()) < 40 or len(value.split()) < 6:
        return False
    return not re.fullmatch(r"(?i)(n/?a|none|todo|tbd|not applicable|no impact|unchanged|because)\W*", value.strip()) and len(set(value.lower().split())) >= 5


def fragment_errors(path: Path) -> list[str]:
    if not re.fullmatch(r"[a-z0-9]+(?:-[a-z0-9]+)*\.(added|changed|fixed|deprecated|removed|security)\.md", path.name):
        return [f"Invalid fragment name {path.name}; use descriptive-slug.category.md"]
    text = path.read_text(encoding="utf-8").strip()
    if not meaningful(text) or text.startswith("#") or re.search(r"\b(TODO|TBD)\b", text):
        return [f"{path}: write a concise user-facing paragraph, without headings or placeholders"]
    return []


def declaration_from_body(body: str) -> dict:
    match = re.search(r"<!-- documentation-impact -->\s*```json\s*\n(.*?)\n```", body, re.S)
    if not match:
        raise ValueError("PR body needs <!-- documentation-impact --> followed by a fenced JSON declaration; see the PR template")
    declaration = json.loads(match[1])
    if not isinstance(declaration, dict):
        raise ValueError("Documentation impact must be a JSON object")
    return declaration


def impact_errors(changed: set[str], declaration: dict, config: dict, root: Path) -> list[str]:
    errors = []
    updated = declaration.get("updated_documents", [])
    exemptions = declaration.get("exemptions", {})
    if not isinstance(updated, list) or not all(isinstance(x, str) for x in updated) or not isinstance(exemptions, dict):
        return ["updated_documents must be a string array and exemptions must map exact document paths to explanations"]
    for document in updated:
        if document not in changed or not (root / document).is_file() or not document.endswith(".md"):
            errors.append(f"Declared updated document is not a changed existing Markdown file: {document}")
    for document, reason in exemptions.items():
        if not meaningful(reason):
            errors.append(f"Exemption for {document} needs a specific explanation of at least 40 characters and six words")
    applicable = [rule for rule in config["rules"] if any(fnmatch.fnmatchcase(path, pattern) for path in changed for pattern in rule["paths"])]
    for rule in applicable:
        for document in rule["documents"]:
            if document not in updated and not meaningful(exemptions.get(document)):
                errors.append(f"{rule['id']}: update and declare {document}, or explain in exemptions why its current explanation remains accurate")
            if document in updated and not (root / document).is_file():
                errors.append(f"{rule['id']}: required document was deleted: {document}")
    for field in ("diagram_impact", "schema_migration_impact"):
        if not meaningful(declaration.get(field)):
            errors.append(f"{field}: explain what changed or why no change is needed (at least 40 characters and six words)")
    changelog = declaration.get("changelog", {})
    if not isinstance(changelog, dict):
        return errors + ["changelog must be an object with fragments or an exemption"]
    fragments = changelog.get("fragments", [])
    if not isinstance(fragments, list) or not all(isinstance(x, str) for x in fragments):
        return errors + ["changelog.fragments must be a string array"]
    for fragment in fragments:
        if fragment not in changed or not fragment.startswith(".changes/") or not (root / fragment).is_file():
            errors.append(f"Changelog fragment must be changed and present under .changes/: {fragment}")
        else:
            errors += fragment_errors(root / fragment)
    if not fragments and not meaningful(changelog.get("exemption")):
        visible = any(rule.get("user_visible") for rule in applicable)
        errors.append(("User-visible changes need a changelog fragment" if visible else "Declare a fragment or a justified documentation/internal-change exemption") + "; see .changes/README.md")
    if any(rule.get("schema") for rule in applicable) and "src/DeDuplicator.Infrastructure/ScanStore.cs" in changed:
        # Structural drift is checked separately against generated output. A semantic declaration is still required.
        if "migration" not in str(declaration.get("schema_migration_impact", "")).lower():
            errors.append("Persistence changes: schema_migration_impact must explicitly address migrations")
    return errors


def changed_files(base: str, head: str, root: Path = ROOT, working: bool = True) -> set[str]:
    commands = [["git", "diff", "--no-renames", "--name-only", "-z", base, head]]
    if working:
        commands += [["git", "diff", "--name-only", "-z", head], ["git", "ls-files", "--others", "--exclude-standard", "-z"]]
    result = set()
    for command in commands:
        output = subprocess.check_output(command, cwd=root).decode("utf-8")
        result.update(path for path in output.split("\0") if path)
    return result


def validate_mermaid(files: list[Path], root: Path = ROOT) -> int:
    diagrams = [(path, index, block) for path in files for index, block in enumerate(re.findall(r"^```mermaid\s*\n(.*?)^```\s*$", path.read_text(encoding="utf-8"), re.M | re.S), 1)]
    if not diagrams:
        raise ValueError("No Mermaid diagrams found; required workflow diagrams must not disappear")
    cli = root / "node_modules/@mermaid-js/mermaid-cli/src/cli.js"
    if not cli.exists():
        raise ValueError("Mermaid CLI missing. Run npm ci first (includes a local Chromium download).")
    temporary_root = root / ".tools/docs"
    temporary_root.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(dir=temporary_root) as folder:
        for number, (source, index, block) in enumerate(diagrams):
            input_file = Path(folder) / f"diagram-{number}.mmd"
            input_file.write_text(block, encoding="utf-8")
            print(f"Mermaid: {source.relative_to(root)} diagram {index}", flush=True)
            run(["node", str(cli), "-i", str(input_file), "-o", str(input_file.with_suffix('.svg')), "--quiet"], root)
    return len(diagrams)


def generated_errors(root: Path = ROOT) -> list[str]:
    errors = []
    for relative, expected in references(root).items():
        path = root / relative
        if not path.exists() or path.read_text(encoding="utf-8") != expected:
            errors.append(f"Generated drift in {relative}; run npm run docs:generate and commit the result")
    return errors


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", default="origin/staging")
    parser.add_argument("--head", default="HEAD")
    parser.add_argument("--pr-body", type=Path)
    parser.add_argument("--event", type=Path, help="GitHub pull_request event JSON; body is read as data, never shell code")
    parser.add_argument("--content-only", action="store_true", help="No impact assessment; not sufficient for a PR")
    args = parser.parse_args()
    errors = []
    config = json.loads((ROOT / "scripts/docs/impact-rules.json").read_text(encoding="utf-8"))
    for document in config["required_documents"]:
        if not (ROOT / document).is_file() or not (ROOT / document).read_text(encoding="utf-8").strip():
            errors.append(f"Missing required documentation: {document}")
    if not args.content_only:
        if args.event:
            event = json.loads(args.event.read_text(encoding="utf-8"))
            pr = event["pull_request"]
            if pr["base"]["ref"] == "main" and pr["head"]["ref"] != "staging":
                errors.append("Release PRs to main must come from staging. Passing checks never authorizes a main integration.")
            args.base, args.head = pr["base"]["sha"], pr["head"]["sha"]
            body = pr.get("body") or ""
        elif args.pr_body:
            body = args.pr_body.read_text(encoding="utf-8")
        else:
            raise ValueError("Supply --pr-body PATH or --event PATH for a full gate; --content-only deliberately skips impact assessment")
        changed = changed_files(args.base, args.head, working=not bool(args.event))
        print(f"Documentation impact: {len(changed)} changed paths against {args.base}")
        errors += impact_errors(changed, declaration_from_body(body), config, ROOT)
    files = markdown_files(ROOT)
    for path in files:
        errors += link_errors(path, ROOT)
    errors += generated_errors()
    for path in (ROOT / ".changes").glob("*.md"):
        if path.name != "README.md":
            errors += fragment_errors(path)
    if errors:
        for error in errors:
            print("ERROR: " + error, file=sys.stderr)
        return 1
    run(["node", str(ROOT / "node_modules/markdownlint-cli2/markdownlint-cli2-bin.mjs")])
    count = validate_mermaid(files)
    impact = "impact assessment SKIPPED (--content-only)" if args.content_only else "impact declaration validated"
    print(f"Documentation passed: {len(files)} Markdown files; {count} Mermaid diagrams; local links/anchors; {impact}; fragments; generated references.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (ValueError, OSError, KeyError, subprocess.CalledProcessError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        raise SystemExit(1)
