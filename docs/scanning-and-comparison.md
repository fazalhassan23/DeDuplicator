# Scanning and comparison

An exact duplicate has the same length and all the same bytes in its primary content stream as a distinct physical file. Names, timestamps, extensions and visible/semantic similarity do not establish equality. Metadata equality is a separate cleanup consideration. See the [pipeline and stability diagrams](diagrams.md#scan-pipeline).

## Enumeration and filtering

[Scanner](../src/DeDuplicator.Infrastructure/Scanner.cs) normalizes selected roots and removes overlapping descendants with case-insensitive path comparisons. It walks depth-first with incremental directory iterators rather than materializing a drive listing. It always rejects reparse ancestors and entries; links and junctions are not followed. Network roots need `AllowNetwork`. Cloud/offline/recall attributes are skipped unless hydration is permitted, but the earlier reparse exclusion still applies. Consent does not make reparse cloud files eligible.

Extension, hidden/system, directory-exclusion and size settings filter candidates; empty files have a separate opt-in. Snapshots are read using [NativeFile](../src/DeDuplicator.Infrastructure/NativeFile.cs). SQLite stores one file row per `(scan, identity)`, so multiple hard-link paths do not become independent copies. Encountered hard links are recorded as issues and review-required snapshots. Expected access, locking, device and read failures are recorded while the scan continues.

## Candidate stages

1. SQLite groups by logical length; only buckets with two or more physical files proceed. Opening metadata does not imply reading singleton contents.
2. With `Sample` enabled and length at least 1 MiB, sample SHA-256 covers deterministic 4 KiB regions at the start, midpoint and end. At this threshold they do not overlap. Only matching sample groups continue. Samples never establish equality.
3. SHA-256 reads the complete remaining file using bounded stream buffers, with one content read at a time. There is no cached-hash shortcut.
4. Hash/length matches are partitioned through complete byte comparisons against representatives. Forced collisions still form separate verified groups. If a representative becomes unreadable or stale, its provisional group is invalidated. Successful comparisons assign a representative row ID; singleton provisional groups are never exposed as duplicate results.

The result means successful byte equality **at comparison time**, not a frozen snapshot of all files in a tree. A previously compared member can change later; cleanup must reopen and compare it again. The SHA-256 value explains candidate selection, while complete byte comparison establishes the verified scan label.

## Stability, cancellation and resource limits

Native reads deny ordinary write/delete sharing. Identity, length, modification time, attributes, link count and review state are checked from the handle before and after reading. Opening a persisted snapshot must match its original identity/metadata. Read failures or detected changes invalidate candidates; there are no automatic retries. Writable mappings, hostile path ancestors, provider semantics and identity reuse remain limitations; [safety](safety-and-recovery.md) is authoritative about those guarantees.

Enumeration, sampling, hashing and verification check cancellation; stream reads take the cancellation token. A blocked native filesystem/provider call is not guaranteed instantly interruptible. Cancelled/failed scans remain historical and rescan-required. There is no pause or checkpoint-based incremental resume.

The scanner uses no unbounded task fan-out or in-memory candidate list. SQLite readers stream size groups and candidates. Byte comparison uses two 128 KiB buffers; normal hashing uses stream-based SHA-256. Progress is throttled to roughly one update per 150 ms. Group totals are finalized on completion; byte totals omit early mismatch/probe reads, so they are activity counters rather than exact I/O accounting. Per-phase persisted timing and million-file performance qualification are not implemented. See [testing and performance](testing.md#performance-measurement).
