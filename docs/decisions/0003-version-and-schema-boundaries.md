# ADR 0003: Explicit prerelease and schema boundary

Status: accepted; numbered transactional schema migrations implemented on 2026-10-29.

## Context

The SDK's implicit version does not describe incomplete initial scope. Application, schema, format and CI versions have different meanings; recovery journals must not be treated as disposable caches.

## Decision

Define the application SemVer in Version.props, initially `0.1.0-alpha.1`, with derived metadata/package/title. Accumulate fragments and set versions during authorized preparation. Use immutable numbered SQL migrations and SQLite user_version for database upgrades; test legacy recognition, newer-schema refusal and rollback on failure. Profile/export formats remain unversioned; receipt JSON uses FormatVersion 1.

## Consequences

Machine-checkable references avoid per-PR bumps, with tested database upgrades but without implying platform data-safety certification. Preserve incompatible journals and provide deliberate migrations. Main integration, immutable tags and publication remain distinct human permissions under [release policy](../versioning-and-releases.md).
