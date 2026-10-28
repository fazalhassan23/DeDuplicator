# Release checklist

Use this record during explicitly requested preparation. Unchecked items remain outstanding.

- [ ] Record intended version/scope and distinguish preparation, specific main integration, tag creation and publication authorization.
- [ ] Use current staging and preserve unrelated work; resolve draft dependencies and actionable review feedback.
- [ ] Review compatibility, schema/profile/export changes, backup/migration instructions and migration tests.
- [ ] Set Version.props and regenerate schema/version/entities; review affected logic diagrams.
- [ ] Preview/assemble fragments, inspect archival changes and regenerate Unreleased content.
- [ ] Update user, installation, troubleshooting and sanitized screenshot material for changed behavior/layout.
- [ ] Run documentation tooling tests, the full gate and relevant application checks; record actual results and omitted qualification.
- [ ] Create a local package with `./scripts/build.ps1 -Package`; verify extraction/startup, scan, cleanup/restore and accessibility on intended Windows targets with generated fixtures.
- [ ] Review security/privacy, package contents and signing status; exclude databases, credentials and real paths.
- [ ] Complete the preparation PR into staging with required checks/reviews and no bypass.
- [ ] Only with specific authorization, integrate the authorized scope through a dedicated staging-to-main PR, verifying its base, checks and reviews.
- [ ] Only with explicit tag/publication authorization, create the immutable tag and publish reviewed notes/package; verify and report links and commit IDs.

There is no automated publishing workflow. This checklist and documentation checks never authorize main, tags or publication. See [versioning](versioning-and-releases.md).
