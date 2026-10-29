# Persistence and data models

## Authoritative structure

The authoritative sources are ordered embedded SQL files in [Migrations](../src/DeDuplicator.Infrastructure/Migrations), applied by [SchemaMigrator.cs](../src/DeDuplicator.Infrastructure/SchemaMigrator.cs). The [generated structural reference](generated/schema-reference.md) lists every column, declared type, NOT NULL flag, default, primary-key position, unique index and foreign key, followed by the exact initializer SQL to preserve CHECK/table-level constraints and PRAGMAs beyond column introspection. The [entity diagram](generated/entities.md) is generated from those migrations too. Run `npm run docs:generate`; CI recreates SQLite in memory and rejects drift without executing application code.

### Tables and lifecycle

| Table | Purpose and values | Logical relationships and retention |
| --- | --- | --- |
| `scans` | `id` scan UUID; `started` UTC round-trip text; `state` workflow text; `profile` serialized ScanProfile JSON | Parent by convention of `files.scan` and `issues.scan`. History displays the newest 100; older rows remain stored. |
| `files` | `id` rowid alias; `scan` scan UUID; `path` full path; `identity` volume/file key; `length` logical bytes; `record` FileRecord JSON; `hash` SHA-256 or NULL; `sample` sample SHA-256 or NULL; `group_id` provisional representative row ID or NULL | A physical identity occurs once per scan through the unique `physical` index. Group membership lives in rows, without a groups table. Provisional singleton groups are hidden from results and cleared on completion. |
| `issues` | `id` rowid alias; `scan` scan UUID; `path` affected path; `reason` error/skip text | Newest inspection is not implied: UI diagnostics show the first 500 in insertion order. All issues remain stored. |
| `journal` | `id` operation UUID; `plan` action-plan UUID; `source` original path; `destination` intended path or NULL; `action` CleanupAction name; `state` transition text; `detail` recovery/error text; `record` original FileRecord JSON; `created` UTC text; `hash` source-content SHA-256, initially empty; `keeper_record` retained FileRecord JSON; `recycle_receipt` optional receipt JSON | Independent recovery snapshots, not references to scan rows. Journal UI displays the newest 1,000 entries. No automatic pruning or cascade deletion exists. |

The original four tables lack explicit NOT NULL and DEFAULT clauses. Application methods normally supply values, but this is not a schema guarantee. `INTEGER PRIMARY KEY` generates integer rowids; SQLite permits NULL values in ordinary TEXT primary-key columns of rowid tables. The new scan_metrics table uses NOT NULL scan/phase/elapsed_ms, composite primary key (scan,phase), nonnegative elapsed_ms CHECK and an enforced scan foreign key. There is no cascade/delete behavior. Do not infer constraints from C# non-nullable declarations.

### Index purposes

`physical(scan, identity)` enforces physical deduplication within a scan. `sizes(scan, length)` supports candidate-size grouping. `hashes(scan, length, hash)` narrows hash candidate lookups. `groups(scan, group_id)` supports result/group queries. SQLite also creates automatic primary-key indexes for the TEXT keys of scans and journal. Full paths are not a uniqueness key.

`scan_metrics` stores accumulated phase milliseconds per scan; database-write timing overlaps phases. `sample_candidates(scan,length,sample)` supports sample lookups. Receipt JSON stores ParsingName, FilePath and FormatVersion=1; unsupported receipt versions are refused. Missing keeper/receipt fields remain NULL for legacy entries.

## Application models are not tables

[Models.cs](../src/DeDuplicator.Core/Models.cs) defines immutable records. `ScanProfile` contains scan settings; `FileRecord` captures path, length, UTC modified time, volume/file identity, link count, attributes and a review flag. These are serialized into the text columns above, without a JSON schema/version field. `DuplicateGroup` is a query result containing member snapshots, full hash and a partial-page flag. `ScanProgress` is an in-memory progress report. `PlannedFile` pairs source and keeper snapshots, action, optional destination and reason; `ActionPlan` adds a UUID, creation time and profile. Plans themselves are not persisted as a table; journal entries snapshot individual operations.

## Transactions, states and recovery

Initialization requests WAL. Every connection enables foreign keys, FULL synchronous mode and a five-second busy timeout. Metadata insertion uses transactions of at most 256 records; other mutations normally use one parameterized statement per transaction. Cancelled enumeration can omit an uncommitted metadata tail; historical results require a complete rescan. A scan row precedes enumeration. Final scan states are Complete, Cancelled/Failed with rescan-required text, or Interrupted after restart.

Journal states include Started, Copied, Staged for recycling, Recycle retained, Recycle outcome requires inspection, Complete, Failed, Interrupted, Restoring, Restored and Restore failed, with descriptive suffixes during startup recovery. The [recovery flow](diagrams.md#quarantine-and-recovery) explains the actual transitions and ambiguous crash windows. Filesystem operations and SQLite updates are not one atomic transaction.

## Migration and compatibility strategy

The current schema is SQLite `user_version=2`. Migration 001 preserves the original four-table schema; 002 adds keeper/recycle evidence, normalizes legacy NULL hashes to empty strings, adds scan_metrics and the sample index. Empty databases apply all migrations. Unversioned nonempty databases must exactly match the original schema objects before upgrading. Unknown legacy and newer versioned schemas fail without committing structural changes. Embedded sequence numbers must be contiguous and match CurrentVersion.

All pending migrations and version stamps run in one transaction. The rollback regression injects a duplicate column during migration 002 and verifies earlier DDL and the version stamp roll back. Legacy journal preservation, unknown-schema refusal, future-version refusal and per-connection durability have regression coverage. No automatic downgrade or backup is provided. Back up the database with its WAL/SHM companions and quarantine contents while the app is stopped before changing builds; never erase the journal as an upgrade workaround. A pre-upgrade backup with the matching old application is the rollback path. No migrations have been published yet; released migrations must never be rewritten. Regenerate references and the entity diagram when adding a migration.
