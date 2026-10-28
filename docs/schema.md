# Persistence and data models

## Authoritative structure

The live initializer is the raw SQL string in [ScanStore.cs](../src/DeDuplicator.Infrastructure/ScanStore.cs). The [generated structural reference](generated/schema-reference.md) lists every column, declared type, NOT NULL flag, default, primary-key position, unique index and foreign key, followed by the exact initializer SQL to preserve CHECK/table-level constraints and PRAGMAs beyond column introspection. The [entity diagram](generated/entities.md) is generated from that initializer too. Run `npm run docs:generate`; CI recreates SQLite in memory and rejects drift without executing application code.

### Tables and lifecycle

| Table | Purpose and values | Logical relationships and retention |
| --- | --- | --- |
| `scans` | `id` scan UUID; `started` UTC round-trip text; `state` workflow text; `profile` serialized ScanProfile JSON | Parent by convention of `files.scan` and `issues.scan`. History displays the newest 100; older rows remain stored. |
| `files` | `id` rowid alias; `scan` scan UUID; `path` full path; `identity` volume/file key; `length` logical bytes; `record` FileRecord JSON; `hash` SHA-256 or NULL; `sample` sample SHA-256 or NULL; `group_id` provisional representative row ID or NULL | A physical identity occurs once per scan through the unique `physical` index. Group membership lives in rows, without a groups table. Provisional singleton groups are hidden from results and cleared on completion. |
| `issues` | `id` rowid alias; `scan` scan UUID; `path` affected path; `reason` error/skip text | Newest inspection is not implied: UI diagnostics show the first 500 in insertion order. All issues remain stored. |
| `journal` | `id` operation UUID; `plan` action-plan UUID; `source` original path; `destination` intended path or NULL; `action` CleanupAction name; `state` transition text; `detail` recovery/error text; `record` original FileRecord JSON; `created` UTC text; `hash` source-content SHA-256, initially empty | Independent recovery snapshots, not references to scan rows. Journal UI displays the newest 1,000 entries. No automatic pruning or cascade deletion exists. |

All declared columns lack explicit NOT NULL and DEFAULT clauses. Application methods normally supply values, but this is not a schema guarantee. `INTEGER PRIMARY KEY` generates integer rowids; SQLite permits NULL values in ordinary TEXT primary-key columns of rowid tables. There are no CHECK or FOREIGN KEY constraints and no enforced cascade/delete behavior. Do not infer constraints from C# non-nullable declarations.

### Index purposes

`physical(scan, identity)` enforces physical deduplication within a scan. `sizes(scan, length)` supports candidate-size grouping. `hashes(scan, length, hash)` narrows hash candidate lookups. `groups(scan, group_id)` supports result/group queries. SQLite also creates automatic primary-key indexes for the TEXT keys of scans and journal. Full paths are not a uniqueness key.

## Application models are not tables

[Models.cs](../src/DeDuplicator.Core/Models.cs) defines immutable records. `ScanProfile` contains scan settings; `FileRecord` captures path, length, UTC modified time, volume/file identity, link count, attributes and a review flag. These are serialized into the text columns above, without a JSON schema/version field. `DuplicateGroup` is a query result containing member snapshots, full hash and a partial-page flag. `ScanProgress` is an in-memory progress report. `PlannedFile` pairs source and keeper snapshots, action, optional destination and reason; `ActionPlan` adds a UUID, creation time and profile. Plans themselves are not persisted as a table; journal entries snapshot individual operations.

## Transactions, states and recovery

Initialization requests WAL and FULL synchronous mode. Mutations use parameterized commands, normally one SQLite connection and implicit transaction per statement. Opening later connections does not explicitly reapply every PRAGMA; no cross-provider durability claim is made. A scan row precedes enumeration. Final scan states are Complete, Cancelled/Failed with rescan-required text, or Interrupted after restart. Historical results require fresh scanning before planning.

Journal states include Started, Copied, Complete, Failed, Interrupted, Restoring, Restored and Restore failed, with descriptive suffixes during startup recovery. The [recovery flow](diagrams.md#quarantine-and-recovery) explains the actual transitions and ambiguous crash windows. Filesystem operations and SQLite updates are not one atomic transaction.

## Migration and compatibility strategy

There are **no released migrations, migration runner or persisted application schema version**. SQLite `user_version` remains 0. `CREATE TABLE IF NOT EXISTS` initializes new databases; it does not add missing columns or migrate incompatible existing tables. Back up the database and quarantine contents before changing builds; do not erase a journal to resolve an upgrade failure. See [troubleshooting](troubleshooting.md#database-startup-errors).

Before shipping any structural change, introduce numbered immutable SQL migrations and an application-controlled schema version, test upgrade/rollback-failure behavior on fixture databases, and document backup and compatibility rules here. Do not retroactively edit a released migration. This is a required future implementation step, not a claim that automatic upgrades work today. Regenerate references and the entity diagram whenever the initializer or migrations change; a new authoritative migration source must also update the extractor and its tests.
