# Security

## Report a vulnerability privately

Do not open a public issue containing exploitable details, file contents, real paths, hashes, credentials or private databases. Private vulnerability reporting was disabled when checked on 2026-10-28. If the repository later enables **Security → Report a vulnerability**, use that private channel. Otherwise open a minimal public issue asking the maintainer to establish a private reporting channel, without vulnerability details. No dedicated reporting email or response deadline has been established; none is implied here. This task does not change the repository's security-reporting settings.

There are no published supported releases yet. The current prerelease does not carry a production support or security-response SLA. Report the commit or application version, affected Windows/filesystem combination and a sanitized reproduction with generated files. Preserve originals while investigating suspected data loss.

## Security model

Processing is local, with no telemetry, automatic elevation or uploads. SQLite and explicit exports contain sensitive paths and hashes. Opening a scanned file is a separate user-confirmed action and may invoke its associated application. Never open untrusted files merely to inspect duplicate results.

[Safety and recovery](docs/safety-and-recovery.md) documents file identity checks, primary-stream-only equality, permission/stream differences, cleanup validation, residual live-tree races and recovery limits. This application is intended for trusted user-owned directory trees; it does not defend against a privileged attacker swapping path ancestors or hostile filesystem providers.

## Automation and dependencies

Documentation CI uses `pull_request`, not `pull_request_target`, on disposable hosted runners with `contents: read`. Checkout credentials are not persisted. It does not receive repository secrets, run release actions, alter protections, approve changes, or write to branches. PR metadata is parsed as data from the event file rather than interpolated into shell commands. Tool dependencies and GitHub Actions revisions are pinned; `npm ci` consumes the lockfile. The Chromium renderer processes PR-authored diagrams on that isolated runner. Do not reuse this workflow with privileged credentials or a self-hosted runner containing sensitive data.

Local tools execute code from the checked-out branch. Review contributions before running local scripts; read-only CI permissions are not a local sandbox. External Markdown URLs are syntax-checked, but the validator does not fetch arbitrary PR-supplied URLs. Report dependency findings through the same private process when disclosure would expose a vulnerability.
