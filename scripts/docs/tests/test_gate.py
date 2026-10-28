from __future__ import annotations

import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import check
import changelog
import generate


class DocumentationGateTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.config = {"rules": [{"id": "scanning", "paths": ["src/**/Scanner.cs"], "documents": ["docs/scanning.md", "docs/diagrams.md"], "user_visible": True}]}
        self.declaration = {
            "updated_documents": [],
            "changelog": {"exemption": "This internal refactor preserves all observed scan results and does not change a public behavior."},
            "diagram_impact": "The existing pipeline still represents the same ordering and failure paths after this internal change.",
            "schema_migration_impact": "No database structures or persisted formats change, so no migration is required for this refactor.",
            "exemptions": {}
        }

    def tearDown(self):
        self.temporary.cleanup()

    def write(self, relative, text):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")
        return path

    def errors(self, changed):
        return check.impact_errors(set(changed), self.declaration, self.config, self.root)

    def test_sample_scanning_pr_without_mapped_docs_fails_specifically(self):
        errors = self.errors(["src/Engine/Scanner.cs"])
        self.assertTrue(any("docs/scanning.md" in error for error in errors))
        self.assertTrue(any("docs/diagrams.md" in error for error in errors))

    def test_justified_exact_document_exemptions_are_supported(self):
        self.declaration["exemptions"] = {path: "The existing ordering, cancellation and error-handling explanation remains accurate because this change only renames private variables." for path in self.config["rules"][0]["documents"]}
        self.assertEqual([], self.errors(["src/Engine/Scanner.cs"]))

    def test_placeholder_exemption_does_not_pass(self):
        self.declaration["exemptions"] = {"docs/scanning.md": "no impact"}
        self.assertTrue(any("Exemption" in error for error in self.errors(["src/Engine/Scanner.cs"])))

    def test_declaration_cannot_claim_unchanged_or_deleted_document(self):
        self.declaration["updated_documents"] = ["docs/scanning.md"]
        errors = self.errors(["src/Engine/Scanner.cs", "docs/scanning.md"])
        self.assertTrue(any("deleted" in error or "not a changed existing" in error for error in errors))

    def test_user_visible_change_requires_fragment_or_substantive_exemption(self):
        self.declaration["changelog"] = {}
        self.assertTrue(any("User-visible changes" in error for error in self.errors(["src/Engine/Scanner.cs"])))

    def test_changed_documents_and_fragment_satisfy_obligations(self):
        documents = self.config["rules"][0]["documents"]
        for path in documents:
            self.write(path, "# Updated explanation\n")
        fragment = ".changes/content-verification.fixed.md"
        self.write(fragment, "Scans now retain changing files for explicit review rather than marking an interrupted read as a verified duplicate.\n")
        self.declaration["updated_documents"] = documents
        self.declaration["changelog"] = {"fragments": [fragment]}
        self.assertEqual([], self.errors(["src/Engine/Scanner.cs", fragment, *documents]))

    def test_malformed_json_declaration_fails(self):
        with self.assertRaises(ValueError):
            check.declaration_from_body("No structured declaration")
        with self.assertRaises(json.JSONDecodeError):
            check.declaration_from_body('<!-- documentation-impact -->\n```json\n{broken}\n```')

    def test_body_is_parsed_as_data_not_executed(self):
        self.declaration["diagram_impact"] = "Literal $(echo unsafe) remains plain JSON data and cannot be substituted as a shell command here."
        body = '<!-- documentation-impact -->\n```json\n' + json.dumps(self.declaration) + '\n```'
        self.assertEqual(self.declaration, check.declaration_from_body(body))

    def test_relative_link_anchor_and_reference_checks(self):
        self.write("docs/target.md", "# Target heading\n\n## Second heading\n")
        good = self.write("README.md", "# Intro\n\n[Guide](docs/target.md#second-heading)\n\n[Again][guide]\n\n[guide]: docs/target.md\n")
        self.assertEqual([], check.link_errors(good, self.root))
        bad = self.write("bad.md", "# Bad\n\n[Wrong](docs/target.md#absent) [Lost](missing.md) [Ref][undefined]\n")
        self.assertEqual(3, len(check.link_errors(bad, self.root)))

    def test_markdown_links_inside_examples_are_ignored(self):
        document = self.write("example.md", "# Example\n\n```text\n[placeholder](does-not-exist.md)\n```\n")
        self.assertEqual([], check.link_errors(document, self.root))

    def test_fragment_name_and_placeholder_validation(self):
        invalid = self.write("bad.unrecognized.md", "A real sentence but an unsupported category must still be refused by the fragment validator.")
        self.assertTrue(check.fragment_errors(invalid))
        placeholder = self.write("good.fixed.md", "TODO")
        self.assertTrue(check.fragment_errors(placeholder))

    def test_schema_extraction_uses_real_sqlite_constraints(self):
        self.write("src/DeDuplicator.Infrastructure/ScanStore.cs", 'cmd.CommandText = """\nCREATE TABLE scans(id TEXT PRIMARY KEY, state TEXT);\nCREATE TABLE files(id INTEGER PRIMARY KEY, scan TEXT);\nCREATE TABLE issues(id INTEGER PRIMARY KEY, scan TEXT);\nCREATE UNIQUE INDEX physical ON files(scan);\n""";')
        reference = generate.schema_reference(self.root)
        self.assertIn("| `state` | `TEXT` | no | none | 0 |", reference)
        self.assertIn("`physical`: (scan); unique: yes", reference)
        self.assertIn("Foreign keys: none", reference)
        self.assertIn("files {", generate.entity_diagram(self.root))

    def test_generator_refuses_an_unknown_authoritative_schema_shape(self):
        self.write("src/DeDuplicator.Infrastructure/ScanStore.cs", "initializer was replaced")
        with self.assertRaisesRegex(ValueError, "authoritative"):
            generate.schema_sql(self.root)

    def test_check_constraints_are_preserved_in_generated_reference(self):
        self.write("src/DeDuplicator.Infrastructure/ScanStore.cs", 'cmd.CommandText = """\nCREATE TABLE scans(id TEXT PRIMARY KEY, state TEXT CHECK(length(state)>0));\n""";')
        self.assertIn("CHECK(length(state)>0)", generate.schema_reference(self.root))

    def test_version_is_semver_and_changelog_assembly_is_previewable(self):
        self.write("Version.props", "<Project><PropertyGroup><Version>0.3.0-rc.1</Version></PropertyGroup></Project>")
        original = "# Changelog\n\n## [Unreleased]\n\n<!-- unreleased:start -->\n<!-- unreleased:end -->\n\n<!-- changelog-fragments -->\n"
        self.write("CHANGELOG.md", original)
        self.write(".changes/example.added.md", "Users can inspect a verified keeper before cleanup so the reviewed plan explains which copy will remain.")
        assembled, fragments = changelog.assemble(self.root, "0.3.0-rc.1", "2027-01-15")
        self.assertIn("## [0.3.0-rc.1] - 2027-01-15", assembled)
        self.assertEqual(1, len(fragments))
        self.assertEqual(original, (self.root / "CHANGELOG.md").read_text())
        self.write("CHANGELOG.md", assembled)
        with self.assertRaisesRegex(ValueError, "already exists"):
            changelog.assemble(self.root, "0.3.0-rc.1", "2027-01-15")
        with self.assertRaisesRegex(ValueError, "match"):
            changelog.assemble(self.root, "0.4.0", "2027-01-15")

    def test_invalid_semver_is_refused(self):
        for value in ["01.0.0", "0.1", "0.1.0-01", "release", "1.0.0+"]:
            self.write("Version.props", f"<Project><PropertyGroup><Version>{value}</Version></PropertyGroup></Project>")
            with self.subTest(value=value), self.assertRaises(ValueError):
                generate.version(self.root)

    def test_actual_git_change_detection_includes_working_files(self):
        def git(*arguments):
            return subprocess.check_output(["git", *arguments], cwd=self.root, stderr=subprocess.DEVNULL)
        git("init", "-b", "staging")
        git("config", "user.name", "Fixture")
        git("config", "user.email", "fixture@example.invalid")
        git("-c", "commit.gpgsign=false", "commit", "--allow-empty", "-m", "baseline")
        base = git("rev-parse", "HEAD").decode().strip()
        self.write("src/Engine/Scanner.cs", "// synthetic changed-file fixture\n")
        changes = check.changed_files(base, "HEAD", self.root)
        self.assertIn("src/Engine/Scanner.cs", changes)
        self.assertTrue(self.errors(changes))

    def test_changed_schema_produces_generated_drift(self):
        schema = 'cmd.CommandText = """\nCREATE TABLE scans(id TEXT PRIMARY KEY);\nCREATE TABLE files(id INTEGER PRIMARY KEY, scan TEXT);\nCREATE TABLE issues(id INTEGER PRIMARY KEY, scan TEXT);\n""";'
        self.write("src/DeDuplicator.Infrastructure/ScanStore.cs", schema)
        self.write("Version.props", "<Project><PropertyGroup><Version>0.1.0-alpha.1</Version></PropertyGroup></Project>")
        for path, text in generate.references(self.root).items():
            self.write(path, text)
        self.assertEqual([], check.generated_errors(self.root))
        self.write("src/DeDuplicator.Infrastructure/ScanStore.cs", schema.replace("scans(id TEXT PRIMARY KEY)", "scans(id TEXT PRIMARY KEY, extra TEXT)"))
        self.assertTrue(any("schema-reference" in error for error in check.generated_errors(self.root)))
        self.assertTrue(any("entities" in error for error in check.generated_errors(self.root)))

    def test_real_markdown_linter_rejects_malformed_fixture(self):
        path = self.write("bad.md", "# Title\n### Skipped heading\n")
        cli = generate.ROOT / "node_modules/markdownlint-cli2/markdownlint-cli2-bin.mjs"
        result = subprocess.run(["node", str(cli), str(path)], cwd=self.root, capture_output=True, text=True)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("MD", result.stdout + result.stderr)

    def test_real_mermaid_renderer_rejects_invalid_syntax(self):
        path = self.write("invalid.mmd", "this is not a mermaid diagram\n")
        cli = generate.ROOT / "node_modules/@mermaid-js/mermaid-cli/src/cli.js"
        result = subprocess.run(["node", str(cli), "-i", str(path), "-o", str(path.with_suffix('.svg')), "--quiet"], cwd=generate.ROOT, capture_output=True, text=True)
        self.assertNotEqual(0, result.returncode)
        self.assertTrue("diagram type" in result.stderr.lower() or "parse error" in result.stderr.lower(), result.stderr)

    def test_release_archival_preserves_links_and_stops_on_collision(self):
        self.write("Version.props", "<Project><PropertyGroup><Version>0.3.0</Version></PropertyGroup></Project>")
        original = "# Changelog\n\n## [Unreleased]\n\n<!-- unreleased:start -->\n<!-- unreleased:end -->\n\n<!-- changelog-fragments -->\n"
        self.write("CHANGELOG.md", original)
        self.write("docs/safety.md", "# Safety\n")
        fragment = ".changes/review.added.md"
        entry = "Users can inspect the keeper before confirming cleanup; the [safety guide](../docs/safety.md) explains the remaining live-file limitations.\n"
        self.write(fragment, entry)
        archived = ".changes/archive/0.3.0/review.added.md"
        self.write(archived, "existing archive")
        with self.assertRaisesRegex(ValueError, "collision"):
            changelog.write_release(self.root, "0.3.0", "2027-01-15")
        self.assertEqual(original, (self.root / "CHANGELOG.md").read_text())
        (self.root / archived).unlink()
        changelog.write_release(self.root, "0.3.0", "2027-01-15")
        self.assertFalse((self.root / fragment).exists())
        self.assertEqual([], check.link_errors(self.root / archived, self.root))
        self.assertEqual([], check.link_errors(self.root / "CHANGELOG.md", self.root))
        self.assertIn("[safety guide](docs/safety.md)", (self.root / "CHANGELOG.md").read_text())

    def test_base_branch_advances_do_not_become_unrelated_pr_changes(self):
        def git(*arguments):
            return subprocess.check_output(["git", *arguments], cwd=self.root, stderr=subprocess.DEVNULL)
        git("init", "-b", "staging")
        git("config", "user.name", "Fixture")
        git("config", "user.email", "fixture@example.invalid")
        git("-c", "commit.gpgsign=false", "commit", "--allow-empty", "-m", "baseline")
        git("branch", "feature")
        self.write("unrelated-base.txt", "another feature\n")
        git("add", "unrelated-base.txt")
        git("-c", "commit.gpgsign=false", "commit", "-m", "base advanced")
        git("switch", "feature")
        self.write("src/Engine/Scanner.cs", "// scan feature fixture\n")
        git("add", "src/Engine/Scanner.cs")
        git("-c", "commit.gpgsign=false", "commit", "-m", "scan change")
        self.assertEqual({"src/Engine/Scanner.cs"}, check.changed_files("staging", "HEAD", self.root, working=False))


if __name__ == "__main__":
    unittest.main()
