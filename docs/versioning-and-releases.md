# Versioning and releases

## Authoritative application version

[Version.props](../Version.props) is imported by Directory.Build.props for all projects. The SDK derives assembly/file/informational metadata; the window title displays informational metadata and packaging evaluates MSBuild `Version`. The private npm package is documentation tooling and has no competing app version. The [generated reference](generated/version.md) is checked for drift.

The explicit initial `0.1.0-alpha.1` identifies the existing unreleased implementation; it does not claim a production 1.0 release. Accumulate changelog fragments and update public versions during release preparation, not every PR.

Semantic Versioning uses MAJOR.MINOR.PATCH: MAJOR for breaking supported contracts/configuration/compatibility, MINOR for compatible functionality, PATCH for compatible fixes. Prerelease examples are `0.3.0-alpha.1`, `0.3.0-beta.1`, and `0.3.0-rc.1`. During 0.x development, MINOR may break compatibility when documented with upgrade steps; PATCH should preserve supported behavior. Alpha/beta/rc status never authorizes silent data loss.

## Distinct version concepts

| Concept | Current behavior |
| --- | --- |
| Application | Shared Version.props SemVer; derived title, assembly metadata and package name. |
| Database schema | Unversioned initializer; user_version 0 and no migration runner. See [schema](schema.md#migration-and-compatibility-strategy). |
| Profile/export formats | Unversioned JSON/CSV with no version field; changes need compatibility notes and tests. |
| CI build | Git SHA and workflow run ID, not an automatic public version bump. SDK informational metadata may append a revision. |

## Prepare a release

Preparation uses a dedicated branch/PR into staging and the [checklist](release-checklist.md). It does not grant authority to update main, tag or publish.

1. Review limitations, compatibility and migrations. Set Version.props deliberately.
2. Review [fragments](../.changes/README.md) and regenerate references.
3. Preview assembly with an explicit version/date, for example after setting that version:

   ```powershell
   python scripts/docs/changelog.py --version 0.3.0 --date 2027-01-15
   ```

4. Add `--write` after reviewing the preview to update CHANGELOG.md and archive consumed fragments. The command rejects mismatched versions, invalid dates, duplicate releases and archive collisions. It never tags, publishes, pushes or merges.
5. Regenerate after archival. Run the full documentation gate and application checks, verify installation against the actual package and update sanitized UI screenshots where applicable.
6. Commit version, changelog, compatibility/migration notes and release documentation together. Satisfy staging checks/reviews.

Example values are not a scheduled or authorized release. Local changelog/archive mutations are not atomic; inspect the Git diff after interruption before retrying. Published sections/migrations are historical records; correct errors transparently without moving tags or concealing changes.

Specific human authorization is required for a dedicated staging-to-main release integration, and separately for creating an immutable tag such as `v0.3.0` or publishing a release/package. Never force-push/move a published tag. Passing documentation automation does not supply authorization. See [Git workflow](git-workflow.md).
