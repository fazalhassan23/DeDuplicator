# Troubleshooting

## Build and documentation tooling

Check `dotnet --info` lists a .NET 10 SDK, not only a runtime. Use [development commands](development.md); the local SDK is optional. Restore/network errors are not successful builds.

Use Node 22+, Python 3.11+ and `npm ci`. Mermaid requires downloaded Chromium; keep `PUPPETEER_CACHE_DIR` consistent if set. Distinguish runner/browser failures from diagram parse errors; errors name the source/fence. Missing mapped docs need the named update or an exact-path justified exemption. Run `npm run docs:generate` for generated drift, then review and commit it. See [testing](testing.md#documentation-gate-and-its-own-tests). Do not weaken checks to conceal failures.

## Skipped files or no duplicates

Check filters, selected roots, exclusions and hidden/system/empty/network settings. Empty files are opt-in. Reparse links/junctions and reparse cloud files always remain skipped even with hydration consent. Hard links are the same physical file, not independent storage. Similar-looking files with different bytes are not exact duplicates.

Access-denied, locked, changed or disconnected files become issues. Do not elevate or change permissions to force access. Close writers if appropriate, reconnect storage and rescan stable files. There are no automatic retries. Group counts finalize at completion and byte counters approximate observed activity.

## Cleanup refusal or partial completion

Rescan stale/historical results, retain a keeper and exclude protected/review-required copies. Identity/content/permission/attribute/stream changes, missing keepers and occupied destinations cause refusals. Inspect individual journal outcomes; do not assume an atomic batch.

Recycle is unavailable without a permanent fallback. Choose quarantine explicitly and rebuild/review. Interrupted cross-volume operations may leave partial/promoted copies plus the original; inspect both before deleting anything. Restore conflicts require explicit resolution. Failed/interrupted entries cannot restore automatically because outcomes can be ambiguous.

## Database startup errors

Stop the app and preserve `%LOCALAPPDATA%\DeDuplicator`, including quarantine and SQLite companions, before investigating. Do not erase recovery journals as an upgrade workaround. There is no migration runner; `CREATE TABLE IF NOT EXISTS` does not add missing columns to older incompatible tables. Keep the matching app/database pair and plan a deliberate migration/manual recovery; never experiment on the only journal copy.

Only one instance per Windows username/session runs. Use its existing window; closing during work requests cancellation and waits before allowing closure. Startup errors are displayed locally without executing cleanup in the error handler.

## Diagnostic material

UI activity/issues/journals contain paths. There is no default persistent log or uploaded telemetry; debug output can contain operation error text. Reproduce using generated fixtures and redact paths, identities and hashes before sharing diagnostics/screenshots. Read [SECURITY.md](../SECURITY.md) before exposing an exploitable issue publicly.

Offscreen rendering/local NTFS tests do not certify interactive desktop, accessibility, high-DPI, cloud/network/removable providers, low disk or million-file behavior. See [limits](known-limitations.md) and [validation](validation.md).
