# Development

## Prerequisites

- Windows 10/11 x64 and a .NET 10 SDK; `global.json` accepts current .NET 10 feature bands from 10.0.100 onward. See [dependency decisions](dependencies.md) for the verified selection.
- Python 3.11+ with its standard-library SQLite for documentation generation and tests.
- Node.js 22+ and npm for pinned Markdown/Mermaid tooling. `npm ci` downloads Chromium for Mermaid into its standard user cache; optionally set `PUPPETEER_CACHE_DIR` to a writable local directory and keep it consistent when checking docs.
- Git; GitHub CLI is optional for creating PRs. No administrator privileges are needed by the application.

## Build, run and package

```powershell
./scripts/build.ps1
dotnet run --project src/DeDuplicator.App -c Release
./scripts/build.ps1 -Package
```

The build script restores, compiles Release with warnings as errors and runs the application tests. It uses `.tools/dotnet/dotnet.exe` when present, otherwise `dotnet` from PATH; the local SDK is optional and excluded from Git. It sets workspace-local SDK home/package cache paths and disables CLI telemetry. Build/test outputs are under normal project bin/obj and `artifacts/test-results`.

Packaging publishes a self-contained x64 folder at `artifacts/DeDuplicator-win-x64` and a ZIP named `DeDuplicator-VERSION-win-x64.zip`, where VERSION is evaluated from the shared MSBuild property. Extract the whole ZIP and run `DeDuplicator.App.exe`. This is unsigned portable packaging, without an installer, auto-update service or published release. Packaging never tags, pushes, publishes or merges. The prerequisite-free runtime does not imply compatibility testing on every Windows version.

## Documentation commands

```powershell
npm ci
npm run docs:generate
npm run docs:test
npm run docs:check -- --base origin/staging --pr-body .tools/pr-body.md
```

The PR body file contains the [structured impact declaration](git-workflow.md#documentation-impact). CI invokes the same entry point with `--event` instead, reading body and exact base/head SHAs from GitHub event JSON. The generated schema/version/Unreleased references must be committed. Handwritten diagrams are syntax-checked by rendering; explanatory text remains subject to human review.

## Structure and debugging

`src/DeDuplicator.Core` holds domain settings and planning; Infrastructure owns scanning, Win32 file access, SQLite, cleanup and exports; App owns WPF resources/views, commands, presentation state and explicit DI. `tests/DeDuplicator.Tests` combines local NTFS, domain and offscreen WPF tests. `scripts/docs` holds the read-only gate, deterministic generators and their tests. [Architecture](architecture.md) covers dependencies and [ADRs](decisions/README.md) explain decisions.

Open `DeDuplicator.slnx` in a .NET 10-capable Windows IDE and select DeDuplicator.App as startup project, or use the CLI. Debug output uses Microsoft logging's Debug provider. Scan issues and cleanup outcomes are also visible in the expandable activity area and journal. Use generated fixtures for debugging destructive operations; never point automated tests at user data. The app permits only one instance per Windows username/session to protect active journal state. Closing during an operation requests cancellation and asks you to wait before closing again.

The current application dependency is [draft PR #1](https://github.com/fazalhassan23/DeDuplicator/pull/1). The documentation branch includes that ancestor so source-based checks can run; it must not be independently merged as a way to bypass that draft's incomplete scope. See [Git workflow](git-workflow.md).
