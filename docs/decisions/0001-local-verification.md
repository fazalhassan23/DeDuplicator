# ADR 0001: Local verification and conservative cleanup

Status: accepted for the unreleased implementation.

## Context

Names/hashes cannot alone prove byte equality; primary-equal files may carry distinct metadata or roles. Large/live trees need bounded resources and explicit change handling.

## Decision

Use Windows .NET/WPF, a WPF-independent domain and SQLite metadata. Narrow by size/sample/SHA-256 and verify all bytes. Keep processing local, retain keepers, review metadata/link/stream differences and revalidate before mutation. Recommend journaled no-overwrite quarantine; refuse recycling under the current handle policy.

## Consequences

Extra verification I/O and per-file persistence can limit throughput. Locks protect ordinary writers while held, not an entire live tree or hostile providers. Recovery is inspectable but not atomic across SQLite/files. See [safety](../safety-and-recovery.md), [schema](../schema.md) and [performance](../performance.md); no unmeasured certification is implied.
