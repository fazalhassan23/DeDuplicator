# Changelog

User-facing changes follow Semantic Versioning. Entries below are generated from reviewed [fragments](.changes/README.md); released sections remain historical records.

## [Unreleased]

<!-- unreleased:start -->

No unreleased entries.

<!-- unreleased:end -->

<!-- changelog-fragments -->

## [0.1.0-alpha.1] - 2026-10-29

### Added

- Find exact duplicate contents across different filenames, review keeper selections, and quarantine or restore redundant files through a durable local journal. Verified Shell recycling is available on qualified NTFS locations; unsupported locations retain staged files for recovery. See [PR #1](https://github.com/fazalhassan23/DeDuplicator/pull/1) and the [safety guide](docs/safety-and-recovery.md).

### Changed

- Contributor changes now carry a documentation-impact declaration, generated schema/version references, reviewed Mermaid diagrams and release fragments. Local and CI checks report missing documentation before staging integration; the initial application is explicitly labeled as a prerelease.

### Fixed

- Keep action-plan execution and journal restore controls reachable when the window is reduced to its minimum size by allowing the workflow area to scroll vertically.
- Scan and safely quarantine files on local exFAT volumes without assuming NTFS ACLs or alternate streams. Real-volume qualification covers verified transfers to NTFS and back; unavailable recycling retains staging for explicit recovery.
- Harden reviewed cleanup with verified recycling and journal restore, locked cross-volume destinations, transactional schema upgrades, validated profiles, atomic exports and busy-state protection; add native-window workflow regressions, phase timing and Windows build/test/package CI.
