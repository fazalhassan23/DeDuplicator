# Safety and recovery

Exact duplicate means equal length and all equal primary-stream bytes in distinct physical files. It says nothing about safe interchangeability. [Scanning](scanning-and-comparison.md) explains verification stages; [diagrams](diagrams.md) show failure and recovery decisions. Hard links collapse to one physical identity and remain review-required. Empty files are opt-in and offer zero logical savings.

## Live filesystem guarantees

Native reads use FILE_SHARE_READ only, denying ordinary writers, replacement, rename and deletion while handles remain held. Identity and relevant metadata are captured from handles, checked after reads, and checked against snapshots before cleanup. Read, permission, device, identity and stability failures invalidate candidates. There are no automatic retries.

This is not a frozen tree. Previously open writable memory mappings, drivers, provider/network semantics, identity reuse, hardware faults and malicious privileged processes remain outside these guarantees. A member may change after comparison finishes. Verified means successfully compared at scan time; historical results are stale and require rescanning.

## Keeper and plan protection

Automatic proposals protect configured/review-required paths and retain a keeper in each displayed subset. Timestamp preferences do not prove correctness. Groups larger than 500 members are inspected and planned in subsets; unreviewed copies remain untouched, potentially leaving several keepers across pages. Overrides cannot create an all-removal plan.

The immutable reviewed plan captures sources, keepers, actions, destinations and reasons. Protection additions after scanning remain enforced. Execution rejects duplicate sources and keepers also scheduled for removal before any mutation. Each operation reopens source/keeper, checks identities/metadata/protection, compares all bytes, requires matching owner/group/DACL fingerprints and attributes, and records the source hash. The keeper stays open throughout; source removal and same-volume rename use the validated handle.

Extra streams, unknown stream enumeration, multiple links, reparse points and encrypted/sparse/compressed/read-only/system files require manual review and cannot be automatically removed. [Built-in and configured protected locations](configuration.md#application-data-and-built-in-protection) are excluded from cleanup. Primary-stream equality does not preserve other information or application roles.

## Quarantine

Quarantine is recommended. Destinations use an operation UUID and random `.file` name; the original path remains in the journal. Nothing is overwritten. Same-volume rename preserves streams/security and does not release allocation.

Cross-volume transfer creates an exclusive `.partial`, copies through bounded buffers, flushes it, compares all destination bytes with the locked source, promotes without overwrite and only then deletes the source through its handle. Failures or cancellation may leave both copies or a partial. Cross-volume copies preserve primary contents; inherited destination ACLs, creation/access times and every application metadata field are not guaranteed. Extra-stream files are refused before cleanup.

The journal records an operation before mutation and its transfer/outcome transitions. SQLite and filesystem mutations cannot be atomic together. A crash after rename/removal but before journal completion is deliberately marked interrupted on restart even if the physical action finished. No batch is atomic and no destructive retry runs automatically.

## Restore and interrupted operations

Restore accepts completed Quarantine entries only. The original path must be vacant and its parent hierarchy must exist without reparse ancestors. The quarantine file must pass review-state, length and recorded SHA-256 checks while held open. Restore uses the same no-overwrite rename/verified-copy path. Occupied paths require explicit user resolution; no alternate name or overwrite is silently selected.

Inspect original, intended destination and `.partial` paths for failed/interrupted entries. Do not delete either copy until contents and application roles are understood. Preserve the database and quarantine folder first. Do not retry a batch blindly. A failed restore can partially complete; the journal requires inspection rather than claiming nothing changed. Ambiguous entries cannot be automatically restored.

## Recycling, deletion and space

Recycling is unavailable. The shell's path-based route is deliberately refused under the current handle safety policy; a journaled failure asks for a new explicit choice. It never falls back to permanent deletion. PermanentDelete requires the advanced option and final reviewed-plan confirmation, uses handle disposition, and is irreversible. Inspect per-file outcomes after any partial batch.

Logical redundant bytes are not promised free storage. Sparse/compressed allocation, links, allocation units, quarantine volume and Recycle Bin retention affect actual allocation. Same-volume quarantine normally releases zero bytes. Allocated disk bytes are not measured.

## Remaining races and providers

Parent reparse checks and destination creation remain path-based; a hostile process can race ancestor swaps. Use trusted user-owned trees rather than adversarial namespaces. Sharing and identity guarantees vary by provider. Reparse traversal is always disabled, including reparse cloud files after hydration consent. Classic volume serial plus 64-bit file IDs are used, not the complete ReFS 128-bit identity format. See [known limitations](known-limitations.md) and the dated [validation record](validation.md); documentation/CI success is not platform certification.
