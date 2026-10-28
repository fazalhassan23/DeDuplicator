# DeDuplicator

A local Windows desktop application for finding exact duplicate primary file contents. Differently named files can match; names, extensions, timestamps and metadata never prove equality. SHA-256 matches are followed by complete byte comparisons.

## Build and run

Use Windows 10/11 x64 and the .NET 10 SDK. A workspace-local SDK is supported at `.tools/dotnet`. No administrator rights are required by the app.

```powershell
./scripts/build.ps1
dotnet run --project src/DeDuplicator.App -c Release
./scripts/build.ps1 -Package
```

The package is `artifacts/DeDuplicator-win-x64.zip`. Extract it to a local folder and run `DeDuplicator.App.exe`. It is self-contained and does not require a separately installed runtime. Unsigned portable distribution is intentional; there is no installer, signing certificate, or automatic update service.

## Workflow

1. Add folders or drives. Configure exclusions, protected paths, extension/size filters and optional empty/hidden/system/network/cloud handling. Save or load JSON profiles.
2. Scan. Enumeration is incremental; metadata is stored in SQLite. Progress reports observed counts, bytes and elapsed time, without invented percentages.
3. Review groups. Search paths, sort groups, inspect timestamps, link/review flags and full paths. The grid displays 25 groups per page and at most 500 members per group. Select a large group and use More copies / Previous copies to inspect all members. Export includes all members without UI limits.
4. Propose keeper choices using shortest path, oldest, newest or preferred directory. Protected and review-required copies are retained. Override individual selections or use Keep selected copy. Choices affect only the current displayed page.
5. Review the immutable action plan, including keeper paths, destinations and reasons. Confirm execution. Quarantine is the default; advanced permanent deletion must be explicitly enabled. The selected action and destination are captured when the plan is built.
6. Inspect the durable journal. Restore completed quarantine operations when the original path is unoccupied. Interrupted entries require manual inspection; both original and intended destination paths remain recorded.

Scanning and cleanup are cancellable. Historical scans retain their original verification information and are explicitly stale. Rescan historical profile restarts enumeration and all comparisons; it does not trust prior hashes. No telemetry, uploads or automatic elevation are used. Opening a scanned file is an explicit user action with a warning.

**Read [safety](docs/safety.md) before cleanup.** Recycle Bin execution currently refuses safely; it never falls back to permanent deletion. Large groups are planned one displayed subset at a time, preserving a keeper within every subset. The original full production specification is not yet satisfied; outstanding features and verification gaps are recorded in [known limitations](docs/known-limitations.md).

See [architecture](docs/architecture.md), [dependency decisions](docs/dependencies.md), and [performance](docs/performance.md). Application data lives in `%LOCALAPPDATA%\DeDuplicator`; paths and hashes in its database are sensitive local data. Diagnostic details stay in the UI; CSV and JSON exports are explicit user actions containing full paths.
