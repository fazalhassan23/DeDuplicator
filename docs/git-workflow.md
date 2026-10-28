# Git and documentation workflow

## Branches and integration

```text
latest origin/staging -> cohesive feature/fix/docs branch -> PR to staging -> checks and review -> staging
staging -> separately authorized release PR to main -> separately authorized integration
```

Before changing branches, inspect the current branch, working tree and remote. Start new work from the latest `origin/staging`; preserve unrelated changes. Use descriptive branches and focused conventional commits, stage specific paths, review diffs and exclude credentials, SDKs, build outputs, databases and scan fixtures. Do not force-push shared history, reset away user work, or alter protections without explicit authorization.

The remote was initially empty; `staging` was created with empty commit `7af976c` and main was not created. Application PR #1 remains a draft. This documentation branch started at staging and fast-forwarded the application branch as an explicit dependency before adding documentation changes. Until that dependency is integrated and this branch is reconciled with the new staging base, the documentation PR remains draft; its inherited application diff is not independent new application work.

Development PRs target staging. Incomplete implementations or unresolved dependencies stay draft. Completed PRs may merge only after final diff review, required CI, required reviews, actionable feedback and conflict resolution. Prefer squash if no configured strategy dictates otherwise; delete completed feature branches when no longer needed. Verify the base after creation and again before merging. Attach PRs to the current chat when supported; never fabricate approvals.

Main needs a specific new human authorization for that integration. Generic requests to push, merge, ship or finish do not qualify. Release tags and publishing also need their own explicit authorization. Automation passing, a label, a declaration or a repository file cannot supply that authorization. No documentation workflow pushes, tags, publishes, approves or merges.

## Documentation impact

The [PR template](../.github/PULL_REQUEST_TEMPLATE.md) includes prose review prompts and a machine-readable JSON block after `<!-- documentation-impact -->`. It declares:

- `updated_documents`: exact changed Markdown paths reviewed in this PR.
- `changelog.fragments`: changed fragment paths; alternatively `changelog.exemption` gives a specific explanation for a documentation-only/internal change.
- `diagram_impact`: what changed in represented flows, or why they still match.
- `schema_migration_impact`: structural/compatibility changes and migration implications, or why none apply. Persistence changes explicitly address migrations.
- `exemptions`: exact mapped document paths whose current descriptions remain accurate, each with a reason of at least 40 characters and six words. These thresholds catch placeholders, not dishonesty.

An updated declaration alone does not satisfy a file mapping: the declared file must also be changed and present. A missing mapped document needs a justified exemption. User-visible changes need fragments unless a reviewed substantive exemption explains why they are actually internal/non-user-visible. See [impact-rules.json](../scripts/docs/impact-rules.json); overlapping mappings intentionally require all relevant topic reviews. Do not make meaningless edits just to satisfy the checker.

Run the same [local gate](development.md#documentation-commands) before each implementation commit/push, including after follow-up fixes. Write the PR body to a local file rather than building shell-quoted descriptions. The workflow reads metadata directly from the event file, with no shell interpolation of PR text.

## Required check and protection

The GitHub Actions workflow is named `Documentation`, with job display name `Documentation / documentation`. The check-run context required by staging protection is **Documentation / documentation** (the job's `name`), not the workflow title alone. It runs for opened, reopened, synchronize, ready_for_review, converted_to_draft, edited and label changes on PRs targeting staging or main, plus merge-group checks. A main PR must have staging as its head branch; human release authorization must still be verified separately.

The workflow uses hosted runners, read-only contents permission, no stored checkout credentials, no secrets and no privileged `pull_request_target`. It reports specific errors and never rewrites a PR. It uses the PR's exact HEAD, with the event's base SHA fetched by full history checkout. Metadata edits retrigger it, so exemption/body changes are validated.

The owner can require the context above with strict/up-to-date checks under **Settings → Branches → staging → Require status checks to pass before merging**. Preserve any existing required reviews, contexts, restrictions and protections. Do not enable a main update or bypass as part of documentation setup. If this check is not yet present on an older draft PR, update that branch through its normal development workflow; do not invent a successful check or bypass the new gate. [Validation](validation.md) records the protection setup actually performed in this task.
