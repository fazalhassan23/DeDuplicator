# Changelog

User-facing changes follow Semantic Versioning. Entries below are generated from reviewed [fragments](.changes/README.md); released sections remain historical records.

## [Unreleased]

<!-- unreleased:start -->

### Added

- Find exact duplicate contents across different filenames, review keeper selections, and quarantine or restore redundant files through a durable local journal. Recycle Bin execution remains unavailable. See [PR #1](https://github.com/fazalhassan23/DeDuplicator/pull/1) and the [safety guide](docs/safety-and-recovery.md).

### Changed

- Contributor changes now carry a documentation-impact declaration, generated schema/version references, reviewed Mermaid diagrams and release fragments. Local and CI checks report missing documentation before staging integration; the initial application is explicitly labeled as a prerelease.

### Fixed

- Harden reviewed cleanup with verified recycling and journal restore, locked cross-volume destinations, transactional schema upgrades, validated profiles, atomic exports and busy-state protection; add native-window workflow regressions, phase timing and Windows build/test/package CI.

<!-- unreleased:end -->

<!-- changelog-fragments -->
