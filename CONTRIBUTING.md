# Contributing

DeDuplicator is an unreleased Windows application with consequential file operations. Read [safety and recovery](docs/safety-and-recovery.md) before changing cleanup behavior. The initial application is still a draft dependency; see [known limitations](docs/known-limitations.md).

## Set up and validate

Install the .NET 10 SDK on Windows, Python 3.11 or later, and Node.js 22 or later. See [development](docs/development.md) for commands and directory layout. Run:

```powershell
npm ci
npm run docs:generate
npm run docs:test
npm run docs:check -- --base origin/staging --pr-body .tools/pr-body.md
./scripts/build.ps1
```

Write your PR description, including the structured declaration from the [PR template](.github/PULL_REQUEST_TEMPLATE.md), to `.tools/pr-body.md` before the full gate. For an ongoing stack, assess the actual PR base, not only its last commit. `npm run docs:check -- --content-only` is useful while drafting but skips impact obligations and is not a passing PR gate. The full command uses the same Python entry point, Markdown linter, generated-reference checks and Mermaid renderer as CI.

## Coding and safety conventions

Use nullable C#, focused methods and explicit domain names. Keep WPF logic out of file operations and introduce interfaces only at useful boundaries. Treat cancellation as normal; report expected filesystem failures without silently swallowing them. Never execute scanned files automatically, upload contents or paths, request elevation, or weaken permissions to force access. Test destructive behavior only against newly generated temporary fixtures.

Preserve a verified keeper, use deterministic tie-breaking, revalidate before mutation, and never silently replace recycling with permanent deletion. New filesystem behavior needs safety tests and an honest account of residual races. Do not claim platform certification or a benchmark result you have not run.

## Documentation is part of implementation

Before each completed change, identify affected topics using [impact-rules.json](scripts/docs/impact-rules.json), update the authoritative explanation, add a [fragment](.changes/README.md) when appropriate, regenerate derived references and update affected diagrams. Run the documentation gate and relevant application tests, then commit the documentation in the same branch. Repeat after additional code pushes.

Schema changes require schema notes, entity diagrams, a migration plan and appropriate migration tests. Numbered transactional migrations now upgrade recognized databases; preserve published migration history and cover both upgrade and failure rollback. Keep application, schema, file-format and CI versions distinct. Record consequential decisions as short [ADRs](docs/decisions/README.md).

## Review and integration

Follow the [Git workflow](docs/git-workflow.md). Use a cohesive branch from current `origin/staging`, focused commits and a PR into staging. Draft incomplete or dependency-blocked work. Do not manufacture trivial PRs or meaningless documentation edits to satisfy a mapping; justify a real exemption instead. Reviewers verify semantics because file mappings cannot do that automatically.

Completed changes require clean local checks, passing required CI, required reviews and resolved actionable feedback. The required status is **Documentation / documentation**. Repository automation has read-only permissions, never approves its own work, and never pushes changes. Main, release tags and publishing require separate explicit user authorization. Use the [release checklist](docs/release-checklist.md) when preparing a release.

Application CI additionally runs the Windows Release build, safety/workflow tests and self-contained package creation with read-only permissions. It retains test reports, performance smoke evidence and a package for 14 days. Passing it does not replace platform qualification or authorize publication.

For filesystem changes, follow [platform qualification](docs/platform-support.md). The real exFAT test is opt-in on an existing volume and reports a skip when absent. Hosted CI retains qualification evidence if supplied; local generated media fixtures and reports remain untracked.
