# User guide

## Install and choose locations

Produce a trusted self-contained x64 package using [installation](../README.md#installation), extract the whole ZIP and run DeDuplicator.App.exe without elevation. The current prerelease is unsigned and has no published download. Start with a small user-owned test folder and prefer quarantine.

Add folders or enter one folder/drive per line. Overlapping roots are scanned once. Configure [settings](configuration.md), exclusions and protections; save/load profiles explicitly. Whole-drive scans can encounter inaccessible locations. The app records issues without forcing access or opening scanned files automatically.

## Scan and inspect

Choose Scan and verify. Phase, activity and observed counts update without invented percentages. Cancel requests cancellation; blocked provider calls can delay it. Cancelled results remain historical and require rescanning, not checkpoint resume.

Different names group only after full byte comparison. Search paths and sort by logical redundant bytes, member count, length or location. Previous/Next shows up to 25 groups per page. Each group shows up to 500 members; select a member and use More copies/Previous copies to inspect additional members. A single-member last subset cannot generate a removal plan on its own.

Inspect full path, bytes, modified UTC and safety flags. Hard-link aliases collapse to one physical member with encountered alias issues. Protected/review-required copies are retained automatically. Copy path and Show folder are explicit actions; Open file warns before invoking the associated application. Open only trusted files.

## Choose keepers and review

Propose by shortest path, oldest, newest or preferred directory; protection/review status takes precedence with deterministic path tie-breaking. Timestamp preferences are not proof of correctness. Override Remove? checkboxes or use Keep selected copy. Each displayed subset retains a keeper and other pages remain untouched.

Review action plan shows source, retained keeper, action, destination and reasons. Select a row for full details; resize/scroll columns. Rebuild after changing selections or action/destination controls: a displayed plan is immutable. A stale or all-removal plan is refused.

Quarantine is recommended. Recycle execution refuses safely and never becomes permanent deletion. PermanentDelete requires the advanced option and final confirmation and cannot be undone. Logical redundant bytes do not promise free storage; same-volume quarantine normally retains allocation.

## Execute and recover

Execution asks for confirmation, then revalidates identities, contents, protection and metadata. Changed/missing/unreadable copies are skipped with reasons. Batches can partially succeed; cancellation can leave source and destination/partial files. Inspect the journal and [safety/recovery instructions](safety-and-recovery.md), never retry blindly.

History and recovery shows recent scans and journal entries. Restore only accepts completed quarantine entries with a vacant original path, existing non-reparse parent hierarchy and matching quarantine content. Occupied paths require explicit resolution without overwrite. Interrupted or failed operations need manual inspection.

Historical results are stale. Rescan historical profile loads saved settings, then a new scan repeats all enumeration/comparisons. Cleanup invalidates the fresh UI state; rescan before another plan. True pause/incremental resume is not implemented.

## Export and diagnostics

Export includes all stored duplicate members beyond visible page limits, to a new CSV or JSON file without overwrite. CSV quotes/neutralizes spreadsheet-formula prefixes. Full paths, identities and hashes are sensitive local information; no upload occurs.

Expand activity/issues for outcomes. Diagnostics shows the first 500 issues, journal the latest 1,000 operations and history the latest 100 scans; older database rows are retained. See [troubleshooting](troubleshooting.md).
