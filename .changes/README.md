# Changelog fragments

Add a concise paragraph to `descriptive-slug.category.md` for each user-visible change. Categories are `added`, `changed`, `fixed`, `deprecated`, `removed` and `security`. Link the relevant PR when available. Explain breaking changes and migration steps in the entry and detailed documentation; avoid raw commit lists. Do not put a heading in a fragment.

Example filename: `quarantine-collision.fixed.md`. Example content:

```text
Quarantine now refuses occupied destination paths and retains the original file, preventing an existing copy from being overwritten. See the related fix PR.
```

Run `npm run docs:generate` to refresh the Unreleased section. Commit both the fragment and generated changelog update. Documentation-only edits and internal refactors may instead give a specific `changelog.exemption` in the [PR declaration](../docs/git-workflow.md#documentation-impact). The gate checks that the explanation is substantive; reviewers decide whether it is justified.

During explicitly authorized release preparation, [assemble](../docs/versioning-and-releases.md#prepare-a-release) fragments into a dated release section and archive them under `.changes/archive/VERSION/`. Archived fragments are historical and are never reassembled. The assembly command previews by default and does not tag, publish, push or merge anything.
