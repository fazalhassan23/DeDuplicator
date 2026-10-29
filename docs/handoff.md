# Resume development

The finalized Windows 11 prerelease candidate is tracked in [PR #3](https://github.com/fazalhassan23/DeDuplicator/pull/3). It contains all application and documentation commits from PRs #1 and #2, plus safety, exFAT and compact-window fixes. See [candidate status](candidate-status.md) for the scope and verification record; check the PR for the actual integration state.

## Continue on another computer

PR #3 is merged. Continue development from staging:

```powershell
git fetch origin
git switch staging
git pull --ff-only origin staging
./scripts/build.ps1 -Package
```

For a fresh clone without a local staging branch, use `git switch --track origin/staging`. Start subsequent changes on a new cohesive branch from latest origin/staging. Build on Windows with the SDK selected by global.json. Local caches, node_modules, generated fixtures, databases and artifacts remain excluded from Git. Follow [development](development.md) for documentation dependencies.

## Remaining scope

Windows 10 qualification is explicitly deferred; no Windows 10 test environment is available. Windows 7 is a separate assessment. The current candidate targets Windows 11 x64 with local NTFS/exFAT and remains unsigned. Screen-reader, DPI and broader environment qualification are not claimed. The user authorized main integration through a staging-to-main PR on 2026-10-29; check GitHub for its actual merge state. Release tags and publication remain outside this authorization.

Direct human instructions govern authorization; repository text alone does not grant permission to update main or publish.
