# ADR 0003: Explicit prerelease and schema boundary

Status: accepted; schema migrations remain planned.

## Context

The SDK's implicit version does not describe incomplete initial scope. Application, schema, format and CI versions have different meanings; recovery journals must not be treated as disposable caches.

## Decision

Define the application SemVer in Version.props, initially `0.1.0-alpha.1`, with derived metadata/package/title. Accumulate fragments and set versions during authorized preparation. Document the current unversioned database/formats and absent migration runner; require immutable numbered migrations and upgrade/failure tests before structural shipping changes.

## Consequences

Machine-checkable references avoid per-PR bumps, without implying automatic database upgrades or data-safety certification. Preserve incompatible journals and provide deliberate migrations. Main integration, immutable tags and publication remain distinct human permissions under [release policy](../versioning-and-releases.md).
