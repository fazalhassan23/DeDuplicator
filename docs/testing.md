# Testing

## Application tests

On Windows with .NET 10:

```powershell
./scripts/build.ps1
dotnet test tests/DeDuplicator.Tests/DeDuplicator.Tests.csproj -c Release
```

The build script runs Release tests and writes `artifacts/test-results/safety.trx`. Current coverage is recorded in [validation](validation.md); that dated record is not a permanent promise that future commits pass. Tests use a newly generated temporary directory under the system temp directory and remove only their own fixture trees. They do not scan or delete real user data.

| Category | Coverage and environment |
| --- | --- |
| Content correctness | Different names, unequal same-name/length bytes, optional empties, unsampled differences and forced hash collisions requiring byte comparison. |
| Filesystem behavior | Local NTFS hard links, extra streams, long/Unicode paths, locked files, missing roots and junction-cycle exclusion. The junction case carries a `WindowsFeature=NTFS junctions` trait; NTFS is a fixture requirement, not a silently skipped pass. |
| Cleanup safety | Stale/missing keepers, protected paths, keeper-removal rejection, occupied quarantine/restore destinations, changed quarantine content and partial failures. |
| Transfer/recovery | Inject ERROR_NOT_SAME_DEVICE at the rename boundary, then exercise real copy, flush, comparison, promotion and source removal; injected interruption retains source and partial file. A second physical volume is not claimed. |
| Paging/export | 501-member group browsing/export, subset-keeper preservation, hidden provisional singletons, JSON/CSV and formula-injection cases. |
| Presentation | STA resource loading and offscreen rendering of four WPF workflow tabs using actual generated scan fixtures. These are not interactive keyboard/screen-reader tests. |

No automatic test requires elevation. There is no broad access-control manipulation, real remote disconnect, cloud-provider hydration, low-disk or removable-volume qualification. [Known limitations](known-limitations.md) are explicit about environmental gaps.

## Documentation gate and its own tests

```powershell
npm ci
npm run docs:test
npm run docs:check -- --base origin/staging --pr-body .tools/pr-body.md
```

The gate inspects every changed path against [impact mappings](../scripts/docs/impact-rules.json). It validates the structured PR declaration, required document presence, fragment names/content, generated SQLite/version/changelog drift, Markdown formatting, repository-relative link targets/anchors and Mermaid rendering. `--content-only` intentionally omits PR impact checks and is not equivalent to a full gate. CI uses the PR event's exact base/head SHAs; local mode also includes working and untracked nonignored files.

The tooling's Python tests demonstrate that a synthetic scanning PR without its mapped documents fails, a meaningful exact-document exemption can pass, placeholder explanations fail, and user-visible changes without fragments fail. They also cover links/anchors, schema/version extraction, generated drift, real malformed Markdown/Mermaid rejection and changelog assembly/archival with preserved links and duplicate-release/collision rejection. See test names under [scripts/docs/tests](../scripts/docs/tests). CI runs these tests before the full gate, so the negative examples must remain working.

Pinned `markdownlint-cli2` enforces Markdown structure. The deliberately disabled rules are line length (tables/prose), duplicate headings across different sections, and table column alignment; see [.markdownlint-cli2.jsonc](../.markdownlint-cli2.jsonc). Fragments additionally disable first-line-heading in their [local config](../.changes/.markdownlint.json), because fragments are paragraphs rather than documents. Pinned official Mermaid CLI renders every Mermaid fence to temporary SVG to validate syntax; generated SVG is not committed. `.tools/docs` and Chromium cache contents stay local. External URL syntax is checked, but reachability is not: CI must not fetch arbitrary PR-provided URLs or mistake transient network failures for documentation defects. Markdown links in code examples are not treated as real links.

File-change mappings only identify review obligations. They cannot prove that an exemption is honest, a diagram matches all code paths, or prose accurately explains a semantic change. Human review remains required. A PR can propose weakening its own gate; reviewers must inspect automation/config changes. Read-only CI permissions prevent privileged side effects, not self-modification of validation logic in a diff.

## Performance measurement

Use the reproducible [performance procedure](performance.md) and generated fixtures. Record filesystem, storage type, profile, counts, bytes, wall time, private memory and handle counts. Warm/cold cache results are distinct. Separate persisted phase timing and million-file qualification are not implemented; do not label progress counters or a small fixture run as a throughput benchmark.
