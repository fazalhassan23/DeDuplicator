# Repository workflow

Repository: [DeDuplicator](https://github.com/fazalhassan23/DeDuplicator)

- Use `staging` for regular development integration. Start each cohesive feature, refactor, documentation or maintenance branch from the latest `origin/staging`. Fixes follow the issue workflow below. Keep tightly coupled changes together when splitting them would break the app.
- Never push changes directly to `main` or `staging`, except initial creation of a missing staging branch. Push dedicated branches and open PRs targeting the appropriate integration branch.
- Creating a missing `staging` branch is the bootstrap exception. Prefer the existing default branch as its starting point. This repository was initially empty, so staging was bootstrapped with an empty root commit; main was not created or updated.
- Review diffs, preserve unrelated work, stage specific files, exclude secrets and generated outputs, and run relevant checks before focused commits. Use clear conventional commit messages where appropriate.
- PR descriptions cover Summary, Changes, Validation, Risks and limitations, and Related work when relevant. Report only checks actually run. Incomplete implementations and unresolved dependencies require draft PRs.
- Inspect the PR base after creation and before merging. Attach created PRs to the current chat when supported. Never fabricate approvals or approve on the user's behalf.
- Completed feature PRs may be merged into staging only after reviewing the final diff, passing required CI, satisfying required reviews, addressing actionable feedback and verifying scope. Respect protections. Prefer regular merges preserving meaningful commits where repository policy permits; respect required squash/rebase policies. Delete merged branches when no longer needed.
- Updating main requires specific authorization except for the standing verified bug-fix authorization below. Generic requests to push, merge, ship or finish do not authorize feature releases. A release integration uses a dedicated staging-to-main PR with authorized scope and required checks/reviews. Tags, package/release publication and production deployment need explicit authorization unless clearly included in the user's instruction.
- Confirm the configured remote, current branch and working tree before pushing or changing branches. Do not discard unrelated work, rewrite published history, force-push shared branches, or alter repository permissions/protections without explicit authorization.
- Verify pushes, PR creation and merges. If blocked by authentication, permissions, CI or reviews, preserve work and report the exact blocker.
- Report each completed unit's scope, branch, commit IDs, PR link, actual validation results and whether it was merged into staging or remains open, with the reason.

## Frequent meaningful commits

- Commit coherent, independently understandable steps throughout development and push completed increments regularly. Prefer multiple focused commits where work separates naturally; keep inseparable implementation, tests and documentation together.
- Correctness and maintainability take priority over commit counts. Avoid knowingly broken intermediate states. Never manufacture activity with empty commits, formatting churn, artificial splitting, backdating or repeated changes/reversions.
- Use the developer's configured Git identity; never change authorship for contribution credit. Do not promise contribution-calendar results or alter integration policy to obtain them.

## Full issue-to-resolution workflow

The user grants standing authorization to create GitHub issues and complete verified bug-fix cycles, including merging scoped fixes into main subject to checks, required reviews and protections. This supersedes the earlier blanket requirement for a new main-merge authorization for every bug fix. It does not authorize feature releases or publication/deployment.

1. Investigate defects found during tests/development or manually reported by the user. Determine expected versus actual behavior, reproduction, impact and affected branches. Distinguish application defects from test, fixture, configuration or infrastructure failures; do not weaken correct tests.
2. Search existing issues/PRs and update duplicates rather than creating redundant issues. New issues include a clear title, expected/actual behavior, reproduction, environment, sanitized evidence, impact and acceptance criteria. Use existing labels where appropriate; do not assign people or promise deadlines without authorization. Follow private security reporting for vulnerabilities; never publish sensitive evidence.
3. For a defect affecting main, create a dedicated fix branch from the latest origin/main and a PR targeting main. For staging-only defects, branch from staging and target staging. Defects introduced by an unmerged PR normally belong in that PR. Never pull unrelated unreleased features into main to deliver a fix.
4. Fix the root cause, add meaningful regression coverage where practical, verify the original failure, run target-appropriate checks, and update documentation/changelog. Record uncertainty for unreproduced issues; never invent evidence or merge speculative fixes. Material scope, compatibility or safety decisions requiring input must be surfaced.
5. Link the issue in commits/PRs. Describe the problem, correction, validation, documentation impact and risks. Use closing keywords only when that merge actually resolves the issue; otherwise explain remaining work.
6. Merge verified main fixes without repeatedly asking permission once scoped, validated, reviewed as required and free of unresolved material concerns. Never bypass protections or failed checks. Preserve work and report exact blockers when integration cannot proceed.
7. After a main fix, check whether staging already has an equivalent correction. Otherwise propagate it using a dedicated branch and PR into staging, choosing merge/cherry-pick consistent with history. Validate it, avoid duplicate/reverted behavior, and merge through normal protections.
8. Verify merges and CI, close or accurately update the issue, and report issue/PR links, validation, main/staging status and remaining work. A merge is not proof of packaging, publication or deployment. Do not claim production resolution for staging-only changes.

## Documentation, automation and versioning policy

- Maintain substantive README, changelog, contributing/security guidance and documentation for architecture, schema/contracts, logic diagrams, configuration, development, testing, Git/issue workflow, versioning/releases, user workflows, troubleshooting and consequential decisions. Adapt existing files instead of duplicating authoritative explanations. Omit inapplicable topics; never invent support/reporting contacts.
- Describe actual behavior and distinguish implemented, experimental and planned functionality. Use real commands, clear examples, relative links, explicit limitations and sanitized data. Update explanations rather than appending contradictory notes.
- Schema/contract changes document fields/types/defaults, constraints/indexes, relationships, validation, compatibility, migrations and rollback limitations. Update relevant Mermaid diagrams, examples and tests. Never silently rewrite released migrations or published contracts.
- Maintain per-PR .changes fragments for user-visible changes or justified exemptions. Use Added/Changed/Fixed/Deprecated/Removed/Security categories where applicable; assemble during release preparation and preserve published history.
- Use Semantic Versioning and one authoritative release version source, distinct from schema/API/file-format/build versions. Document prereleases and 0.x compatibility. Do not bump release versions for every commit/PR. Release tags are immutable when authorized.
- Automatically update affected documentation with each implementation increment and follow-up PR push. Regenerate deterministic references and run local/CI checks. Agents maintain semantic explanations; file-change checks cannot prove accuracy.
- Maintain PR/issue templates and readable documentation-impact mappings. CI checks relevant code/tests/builds, Markdown, links/anchors, Mermaid, generated drift, changelog/exemptions, version consistency and impact declarations on relevant PR events. Exemptions need meaningful reasons, not artificial document edits.
- Respect existing required checks and report owner configuration needed. Use least-privilege CI; never execute untrusted PR code with privileged credentials. CI must not silently rewrite branches, approve itself, bypass protections or publish without authorization.
- Release preparation updates changelog/version, compatibility/migrations, installation instructions, outdated examples/screenshots and release notes, and runs validation. Prepare via a branch/PR into staging; main integrations and publication follow the authorization rules above.
- Definition of done includes implementation, relevant passing checks, accurate docs/diagrams/schema, changelog or exemption, meaningful commits, correct remote branch/PR and authorized integration. Reports distinguish local, committed, pushed, PR-open, merged, published and deployed states. Verified main bugs also require staging synchronization and accurate issue resolution.

## Local validation

Use `./scripts/build.ps1` on Windows with .NET 10. Use `-Package` for self-contained win-x64 packaging. `.tools/`, `artifacts/`, `bin/`, `obj/`, databases and benchmark data must remain untracked. Refer to `docs/known-limitations.md` before marking the application complete or a draft PR ready for review.

## Documentation definition of done

Every feature, fix or behavioral change updates its authoritative documentation in the same branch, including follow-up pushes. Review `scripts/docs/impact-rules.json`, update affected diagrams, add a user-facing `.changes/` fragment or a substantive declared exemption, regenerate references with `npm run docs:generate`, and run `npm run docs:test` plus `npm run docs:check -- --base origin/staging --pr-body .tools/pr-body.md` before committing. Schema changes also address migrations/compatibility and migration tests. The full PR description must contain the structured Documentation impact declaration from the template. `--content-only` is not a full PR gate. See CONTRIBUTING.md for setup and authoritative topic links. Documentation CI cannot prove semantic accuracy or authorize main, tags or releases.
