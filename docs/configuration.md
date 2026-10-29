# Configuration

Settings are serialized as `ScanProfile` JSON using the exact property names in [Models.cs](../src/DeDuplicator.Core/Models.cs). The UI saves/loads profiles explicitly; there is no automatic global settings file. Sample [all-local-files](../profiles/all-local-files.json) and [documents](../profiles/documents.json) profiles intentionally have empty Roots: choose your own locations before scanning.

| Property | Default | Effect |
| --- | --- | --- |
| `Roots` | `[]` | Folders or drives; normalize and remove overlapping selections. UI requires at least one. |
| `ExcludedDirectories` | `[]` | Skip matching directory paths and descendants during traversal. |
| `ProtectedDirectories` | `[]` | Retain matching paths from automatic selection; protect again during cleanup. |
| `IncludedExtensions` | `[]` | Empty means all; otherwise compare extensions case-insensitively. UI accepts comma-separated values and supplies a leading dot. |
| `ExcludedExtensions` | `[]` | Exclude listed extensions even if included. |
| `MinimumSize` | `1` | Minimum logical nonempty length, in bytes. UI rejects negative sizes. |
| `MaximumSize` | `9223372036854775807` | Largest nonempty length; empty UI maximum uses this value. |
| `IncludeEmpty` | `false` | Allow zero-length candidates independently of the nonempty minimum; zero logical savings. |
| `IncludeHidden` | `false` | Include hidden entries and allow traversal of hidden directories. |
| `IncludeSystem` | `false` | Include system entries; protected cleanup rules still apply. |
| `AllowNetwork` | `false` | Permit UNC roots; this does not establish provider guarantees. |
| `AllowCloudHydration` | `false` | Permit offline/recall-marked non-reparse entries; all reparse entries remain excluded. |
| `Sample` | `true` | Use the optional large-file sample elimination stage; never substitute it for full verification. |

Example, using a fictional location:

```json
{
  "Roots": ["D:\\ExampleLibrary"],
  "ProtectedDirectories": ["D:\\ExampleLibrary\\Originals"],
  "ExcludedDirectories": ["D:\\ExampleLibrary\\Cache"],
  "IncludedExtensions": [".pdf", ".txt"],
  "MinimumSize": 1,
  "IncludeEmpty": false,
  "AllowNetwork": false,
  "AllowCloudHydration": false,
  "Sample": true
}
```

Profile loading rejects null lists, invalid size ranges, empty path entries and wildcard/path extensions; literal extensions are normalized and deduplicated. The UI limits loaded profile files to 1 MiB. Sample profiles may be loaded without roots, but scanning requires a root.

## Fixed policies and cleanup preferences

There is one conservative I/O profile, with serial content reads and bounded comparison buffers. Link traversal is fixed to skip; neither option is configurable today. Sampling thresholds/buffer sizes are internal constants described in [scanning](scanning-and-comparison.md).

Keeper preferences are `ShortestPath`, `Oldest`, `Newest` and `PreferredDirectory`, with deterministic case-insensitive path tie-breaking. These choices are UI/action-plan state, not saved ScanProfile fields. Protected/review-required members take precedence and cannot be automatically removed. Timestamps express a preference, never proof that one copy is more correct.

Cleanup defaults to `Quarantine`. `Recycle` first transfers to the reviewed quarantine location, then uses the Windows recycling-only Shell method. An unsupported location retains staging for recovery; it never becomes permanent deletion. `PermanentDelete` requires the advanced checkbox and the final confirmation. Quarantine defaults to `%LOCALAPPDATA%\DeDuplicator\Quarantine`; choose a different destination before rebuilding the plan if needed. A plan captures its action and destination when built. Protected directories added after a scan are unioned with scan-time protections during planning/execution, so removing a textbox entry cannot silently remove an earlier protection from that plan.

## Application data and built-in protection

An exclusive .instance.lock file lease prevents two app instances, including across Windows sessions, from running startup recovery against the same data directory. The empty lock file persists after exit and is not evidence of a running process; the open handle is the lock. The database is `%LOCALAPPDATA%\DeDuplicator\scans.db`; its WAL/SHM companions may exist. It stores paths, identities, hashes, profiles and operation recovery snapshots. The app creates no default log file or network sink; debug logging and UI details may include error paths. Explicit CSV/JSON exports contain full paths and should be handled as sensitive local data.

Windows, Program Files, Program Files (x86), ProgramData, roaming application data, local Packages/Programs and DeDuplicator's own local data directory are built-in protected locations. Protected status affects cleanup, not an automatic enumeration exclusion. User exclusions prevent scanning; protections preserve copies after scanning. Long paths and Unicode use native Windows support, but filesystem/provider limits still apply. See [safety](safety-and-recovery.md) and [troubleshooting](troubleshooting.md).

Local exFAT needs no special scan setting: handle capability checks recognize absent persistent ACLs/named streams. Quarantine remains recommended, and recycling can retain staging when unavailable. Supported OS scope and real-volume test commands are in [platform support](platform-support.md).
