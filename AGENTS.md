# Repository workflow

Repository: https://github.com/fazalhassan23/DeDuplicator

- Use `staging` for integration. Start each cohesive feature, fix, refactor or documentation branch from the latest `origin/staging`. Keep tightly coupled changes together when splitting them would break the app.
- Never push development changes directly to `main` or `staging`. Push dedicated feature branches and open PRs targeting `staging`.
- Creating a missing `staging` branch is the bootstrap exception. Prefer the existing default branch as its starting point. This repository was initially empty, so staging was bootstrapped with an empty root commit; main was not created or updated.
- Review diffs, preserve unrelated work, stage specific files, exclude secrets and generated outputs, and run relevant checks before focused commits. Use clear conventional commit messages where appropriate.
- PR descriptions cover Summary, Changes, Validation, Risks and limitations, and Related work when relevant. Report only checks actually run. Incomplete implementations and unresolved dependencies require draft PRs.
- Inspect the PR base after creation and before merging. Attach created PRs to the current chat when supported. Never fabricate approvals or approve on the user's behalf.
- Completed feature PRs may be merged into staging only after reviewing the final diff, passing required CI, satisfying required reviews, addressing actionable feedback and verifying scope. Respect protections. Prefer squash merging when no strategy is specified. Delete merged feature branches when no longer needed.
- Updating main always requires a new, explicit instruction authorizing that specific integration. Generic requests to push, merge, ship or finish are insufficient. A release uses a dedicated staging-to-main PR with the authorized scope and required checks/reviews.
- Confirm the configured remote, current branch and working tree before pushing or changing branches. Do not discard unrelated work, rewrite published history, force-push shared branches, or alter repository permissions/protections without explicit authorization.
- Verify pushes, PR creation and merges. If blocked by authentication, permissions, CI or reviews, preserve work and report the exact blocker.
- Report each completed unit's scope, branch, commit IDs, PR link, actual validation results and whether it was merged into staging or remains open, with the reason.

## Local validation

Use `./scripts/build.ps1` on Windows with .NET 10. Use `-Package` for self-contained win-x64 packaging. `.tools/`, `artifacts/`, `bin/`, `obj/`, databases and benchmark data must remain untracked. Refer to `docs/known-limitations.md` before marking the application complete or a draft PR ready for review.
