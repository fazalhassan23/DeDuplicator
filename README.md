# DeDuplicator

A local Windows application for finding exact duplicates by primary file contents. Different names can match; equal names, timestamps or extensions never prove equality. SHA-256 candidates require complete byte verification.

The current [version](docs/generated/version.md) is an unreleased alpha. The consolidated implementation and finalization are tracked in [PR #3](https://github.com/fazalhassan23/DeDuplicator/pull/3); earlier PRs #1 and #2 are included in its history. Reviewed quarantine, real Windows recycling and journal restore pass fixture tests; broader platform qualification remains incomplete. Read [limitations](docs/known-limitations.md) before using real files.

The current candidate scope is Windows 11 x64 with local NTFS/exFAT; Windows 10 qualification is deferred and Windows 7 is a separate assessment. See [candidate status](docs/candidate-status.md) for verified checks and remaining limitations.

## Capabilities

- Incremental folder/drive scanning with filters, protections, physical identities and SQLite history; local NTFS/exFAT qualification is recorded in the platform matrix.
- Sample elimination, complete SHA-256 and byte verification, with cancellation and per-file issues.
- Paged results, deterministic keeper proposals, overrides and reviewed immutable plans.
- Revalidated no-overwrite quarantine, verified cross-volume copy/removal and journal-based validated recovery.
- Windows Recycle Bin with a recoverable staging step and journal receipt; explicit advanced permanent deletion with no silent fallback.
- Streaming CSV/JSON exports with formula protection; no content/path/hash upload or telemetry.

The [user guide](docs/user-guide.md) explains the workflow and [safety/recovery](docs/safety-and-recovery.md) defines guarantees. Offscreen WPF renders are generated locally in `artifacts/ui` and excluded from Git because they contain local fixture paths. No sanitized public screenshot or interactive desktop certification is supplied yet.

## Installation

No published release/download or signed installer exists yet. Use a supported Windows 11 x64 edition/build from the [platform matrix](docs/platform-support.md) and install a .NET 10 SDK and produce an unsigned portable self-contained package:

```powershell
./scripts/build.ps1 -Package
```

Extract `artifacts/DeDuplicator-VERSION-win-x64.zip` and run DeDuplicator.App.exe from its complete folder. VERSION derives from Version.props. No separately installed runtime is needed. Do not elevate or change permissions to force access.

## Quick start from source

```powershell
./scripts/build.ps1
dotnet run --project src/DeDuplicator.App -c Release
```

Choose a small user-owned folder, scan, inspect verified groups, propose/override keepers, then review and confirm quarantine. Inspect the journal and restore only to vacant original paths. Historical results require rescanning. Logical redundant bytes are not promised free space; same-volume quarantine normally retains allocation.

## Documentation

| Audience | Guides |
| --- | --- |
| Users | [Workflow](docs/user-guide.md), [configuration](docs/configuration.md), [troubleshooting](docs/troubleshooting.md), [safety](docs/safety-and-recovery.md) |
| Contributors | [Contributing](CONTRIBUTING.md), [development](docs/development.md), [testing](docs/testing.md), [Git workflow](docs/git-workflow.md) |
| Maintainers | [Architecture](docs/architecture.md), [schema](docs/schema.md), [scanning](docs/scanning-and-comparison.md), [diagrams](docs/diagrams.md), [ADRs](docs/decisions/README.md) |
| Releases | [Changelog](CHANGELOG.md), [versioning](docs/versioning-and-releases.md), [checklist](docs/release-checklist.md) |
| Security and qualification | [Security](SECURITY.md), [validation](docs/validation.md), [limits](docs/known-limitations.md), [performance](docs/performance.md) |

Documentation accompanies implementation and is enforced locally and in CI. See contributing for the shared command and justified exemptions. Automation never authorizes main integration, tagging or publication.
