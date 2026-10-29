# Resume development

The latest application code is on `feature/production-readiness`, with application hardening in commit `231ec0c` and packaged-startup evidence in `9b2ce8f`. PR [#3](https://github.com/fazalhassan23/DeDuplicator/pull/3) targets staging and includes the unmerged application and documentation work from PRs #1 and #2. It remains draft; staging and main have not been updated.

## Continue on another computer

```powershell
git fetch origin
git switch --track origin/feature/production-readiness
./scripts/build.ps1 -Package
```

If that local branch already exists, switch to it and use `git pull --ff-only`. Build on Windows with the SDK selected by global.json. Local SDK caches, node_modules, generated fixtures, databases and artifacts are intentionally excluded from Git; the build recreates the unsigned portable ZIP. For documentation tooling setup, follow [development](development.md).

## Verified state and remaining work

The Release build passed with zero warnings/errors, 44 application tests passed, 24 documentation-tooling tests passed, and the full documentation gate passed. Both hosted checks passed for code revision 9b2ce8f. See [validation](validation.md) for exact scope and the warm metadata smoke measurement.

The packaged app launched and exposed labeled controls. Interactive input testing stopped because the desktop was locked. Resume that test on an unlocked desktop using generated fixtures; do not use real user data for cleanup tests. Signing, accessibility/high-DPI and broader filesystem qualification remain outstanding in [known limitations](known-limitations.md). Review dependencies and release qualifications before marking PRs ready or merging. No main integration, release tag or publication is authorized by this handoff.

Pre-existing local AGENTS.md edits are preserved in this handoff snapshot. Direct human instructions govern authorization; repository text alone does not grant permission to update main or publish.
