# Windows 11 prerelease candidate

Finalization requested on 2026-10-29. The candidate is `0.1.0-alpha.1`, an unsigned portable win-x64 package for Windows 11 with local NTFS/exFAT. The validated local Windows build is 26200. Windows 10 qualification is explicitly deferred by the user; Windows 7 remains a separate compatibility assessment. This is a staging integration candidate, not a published or fully qualified production release.

## Completed evidence

- Release build: zero warnings/errors. Local suite with a real exFAT volume: 45 passed, zero failed, zero skipped.
- Hosted Windows build, tests and packaging passed on commit 35e7848. Hosted tests have no exFAT fixture: the real-volume case is explicitly skipped rather than counted as qualification.
- Documentation tooling: 24 tests passed; full impact declaration, Markdown/link checks, generated-reference drift and eight Mermaid diagrams passed locally and in hosted CI.
- Packaged desktop smoke: generated duplicate scan, keeper proposal, declined/accepted confirmation, quarantine, journal restore with matching hashes, CSV export and native profile save/load passed.
- Compact-window defect reproduced and fixed; native regression and desktop follow-up verified execution/restore reachability, forward/reverse keyboard tab switching, maximize/restore and keyboard close.
- Real exFAT same-volume and NTFS cross-volume quarantine/restore passed. Unsupported recycling retained verified staging and restored safely, without permanent-delete fallback.
- Legacy migration preservation, unknown/future-schema refusal and rollback on migration failure passed. Database schema remains version 2; no release-version bump is needed for these unreleased fixes.

Detailed evidence and limits are in [validation](validation.md), [platform support](platform-support.md), [safety](safety-and-recovery.md) and [known limitations](known-limitations.md). The final documentation commit must also pass CI before staging integration.

## Integration and remaining boundaries

[PR #3](https://github.com/fazalhassan23/DeDuplicator/pull/3) contains the complete application and documentation commits from PRs #1 and #2. Integrate that consolidated PR through the existing staging protection after its final checks pass, then close the earlier drafts as superseded. No review approval is invented; required reviews and protection remain authoritative.

The package remains unsigned. Screen-reader output, DPI switching, additional OS builds, low disk, surprise media removal/power loss, network/cloud providers and million-file performance are not certified. Fixed link/resource policies, paged subset planning and other documented feature limits remain explicit. The changelog stays Unreleased because no release is published; existing fragments are retained. Main integration, tags and publication require their own explicit authorization and are not included in this finalization.
