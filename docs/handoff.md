# Resume development

The finalized Windows 11 prerelease candidate is tracked in [PR #3](https://github.com/fazalhassan23/DeDuplicator/pull/3). It contains all application and documentation commits from PRs #1 and #2, plus safety, exFAT and compact-window fixes. See [candidate status](candidate-status.md) for the scope and verification record; check the PR for the actual integration state.

## Continue on another computer

After PR #3 is merged, use staging:

```powershell
git fetch origin
git switch staging
git pull --ff-only origin staging
./scripts/build.ps1 -Package
```

For a fresh clone without a local staging branch, use `git switch --track origin/staging`. Before the merge, the complete candidate remains on feature/production-readiness. Start subsequent changes on a new cohesive branch from latest origin/staging. Build on Windows with the SDK selected by global.json. Local caches, node_modules, generated fixtures, databases and artifacts remain excluded from Git. Follow [development](development.md) for documentation dependencies.

## Remaining scope

Windows 10 qualification is explicitly deferred; no Windows 10 test environment is available. Windows 7 is a separate assessment. The current candidate targets Windows 11 x64 with local NTFS/exFAT and remains unsigned. Screen-reader, DPI and broader environment qualification are not claimed. No main integration, release tag or publication is included in this handoff.

Direct human instructions govern authorization; repository text alone does not grant permission to update main or publish.
