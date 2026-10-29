# Platform support and qualification

The first-release target is Windows 10/11 x64 with local NTFS and exFAT. This is a qualification scope, not a claim that every edition, build or device has been tested. The package is an unsigned prerelease. Windows 7 is a separate compatibility assessment and is not supported by the current .NET 10 package.

## Operating systems

Microsoft's [.NET on Windows table](https://learn.microsoft.com/dotnet/core/install/windows) lists supported Windows 11 versions and Windows 10 LTSC/Enterprise versions. Verify the exact edition/build against that table before release; do not promise all Windows 10 installations. .NET 10 does not support Windows 7. A self-contained package includes the runtime but does not extend its operating-system compatibility. Legacy support would require a separately reviewed runtime/dependency/native API strategy and tests on Windows 7; no such build exists today.

Local automated and desktop smoke evidence is from Windows build 26200. A separate supported Windows 10 machine/VM and additional Windows 11 target builds remain qualification requirements. CI Windows runners add build/test evidence, not a substitute for that matrix. See [validation](validation.md).

## Filesystems

| Local filesystem | Implemented policy | Verified scope |
| --- | --- | --- |
| NTFS | Handle identity/stability, ACL fingerprints, alternate-stream review, hard-link deduplication, byte verification and reviewed cleanup. | Existing automated suite and packaged desktop smoke. |
| exFAT | Query volume capabilities through the open file handle. Do not require ACL fingerprints or enumerate named streams when the filesystem cannot persist them. Distinguish absent persistent ACLs from NTFS security. | Real local exFAT volume: distinct identities, Unicode paths, sampling/hash/byte verification, same-volume quarantine/restore, actual transfers to NTFS and back, explicit permanent-delete opt-in, and safe retained staging when recycling is unavailable. |
| FAT32, ReFS and other providers | Capability detection is present, but this is not an initial support claim. Missing usable identities or unreadable security/stream state fail conservatively. | No dedicated qualification in this release scope. |

Microsoft documents the [filesystem capability differences](https://learn.microsoft.com/en-us/windows/win32/fileio/filesystem-functionality-comparison) and [handle-based volume query](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getvolumeinformationbyhandlew). Unknown providers that reject that query retain conservative security/stream checks; failed queries never imply absent security. Cross-capability source/keeper security differences require review. An NTFS quarantine destination can inherit different ACLs; see [safety](safety-and-recovery.md).

Recycling is conditional on Windows support for the location. The tested exFAT location returned Recycle retained; the verified staged copy restored successfully. This is not evidence of successful exFAT Shell recycling. No automatic permanent-deletion fallback exists. Device removal, surprise power loss, full media, hostile directory swaps and broad removable-device qualification remain outstanding.

## Run real exFAT qualification

Use an existing exFAT volume/directory; the test never formats, mounts or enumerates existing user files. It creates unique DeDuplicator-qualification fixture directories and removes only those owned directories. Its database and cross-volume destination use the normal temporary directory, which must be on NTFS for this test.

```powershell
$env:DEDUPLICATOR_EXFAT_ROOT = 'D:\'
./scripts/build.ps1 -Package
Remove-Item Env:DEDUPLICATOR_EXFAT_ROOT
```

Choose the actual exFAT location on that computer. The test fails if it is not exFAT or the temporary volume is not NTFS. Without the environment variable, the real-volume test is explicitly skipped; the skip is not a qualification pass. Hosted CI has no configured exFAT fixture and reports that skip. Local reports are `artifacts/test-results/exfat.trx`, `artifacts/test-results/safety.trx` and `artifacts/qualification/exfat.txt`; generated artifacts are not source-controlled.
