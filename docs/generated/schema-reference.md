# Generated SQLite structure

Generated from the ordered SQL migrations in `src/DeDuplicator.Infrastructure/Migrations` (legacy inline SQL is supported only for fixtures).
Do not edit this reference; run `npm run docs:generate`. Explanations and lifecycle rules are in [schema](../schema.md).

## files

| Column | SQLite type | NOT NULL clause | Default | PK position |
| --- | --- | --- | --- | --- |
| `id` | `INTEGER` | no | none | 1 |
| `scan` | `TEXT` | no | none | 0 |
| `path` | `TEXT` | no | none | 0 |
| `identity` | `TEXT` | no | none | 0 |
| `length` | `INTEGER` | no | none | 0 |
| `record` | `TEXT` | no | none | 0 |
| `hash` | `TEXT` | no | none | 0 |
| `sample` | `TEXT` | no | none | 0 |
| `group_id` | `INTEGER` | no | none | 0 |

### Indexes

- `groups`: (scan, group_id); unique: no; origin: `c`.
- `hashes`: (scan, length, hash); unique: no; origin: `c`.
- `physical`: (scan, identity); unique: yes; origin: `c`.
- `sample_candidates`: (scan, length, sample); unique: no; origin: `c`.
- `sizes`: (scan, length); unique: no; origin: `c`.

Foreign keys: none.

## issues

| Column | SQLite type | NOT NULL clause | Default | PK position |
| --- | --- | --- | --- | --- |
| `id` | `INTEGER` | no | none | 1 |
| `scan` | `TEXT` | no | none | 0 |
| `path` | `TEXT` | no | none | 0 |
| `reason` | `TEXT` | no | none | 0 |

### Indexes

No indexes beyond the INTEGER PRIMARY KEY rowid alias, when present.

Foreign keys: none.

## journal

| Column | SQLite type | NOT NULL clause | Default | PK position |
| --- | --- | --- | --- | --- |
| `id` | `TEXT` | no | none | 1 |
| `plan` | `TEXT` | no | none | 0 |
| `source` | `TEXT` | no | none | 0 |
| `destination` | `TEXT` | no | none | 0 |
| `action` | `TEXT` | no | none | 0 |
| `state` | `TEXT` | no | none | 0 |
| `detail` | `TEXT` | no | none | 0 |
| `record` | `TEXT` | no | none | 0 |
| `created` | `TEXT` | no | none | 0 |
| `hash` | `TEXT` | no | none | 0 |
| `keeper_record` | `TEXT` | no | none | 0 |
| `recycle_receipt` | `TEXT` | no | none | 0 |

### Indexes

- `sqlite_autoindex_journal_1`: (id); unique: yes; origin: `pk`.

Foreign keys: none.

## scan_metrics

| Column | SQLite type | NOT NULL clause | Default | PK position |
| --- | --- | --- | --- | --- |
| `scan` | `TEXT` | yes | none | 1 |
| `phase` | `TEXT` | yes | none | 2 |
| `elapsed_ms` | `INTEGER` | yes | none | 0 |

### Indexes

- `sqlite_autoindex_scan_metrics_1`: (scan, phase); unique: yes; origin: `pk`.

Foreign keys: present.

## scans

| Column | SQLite type | NOT NULL clause | Default | PK position |
| --- | --- | --- | --- | --- |
| `id` | `TEXT` | no | none | 1 |
| `started` | `TEXT` | no | none | 0 |
| `state` | `TEXT` | no | none | 0 |
| `profile` | `TEXT` | no | none | 0 |

### Indexes

- `sqlite_autoindex_scans_1`: (id); unique: yes; origin: `pk`.

Foreign keys: none.

## SQLite constraint interpretation

`INTEGER PRIMARY KEY` aliases rowid and generates an integer when NULL is inserted. Ordinary TEXT primary keys in rowid tables can accept NULL unless explicitly constrained. NOT NULL/default/foreign-key information is reflected above; the exact initializer below also preserves CHECK expressions, table-level constraints and PRAGMAs that column introspection cannot fully describe. Application non-nullability is not a database constraint.

## Authoritative initializer SQL

```sql
CREATE TABLE IF NOT EXISTS scans(id TEXT PRIMARY KEY, started TEXT, state TEXT, profile TEXT);
CREATE TABLE IF NOT EXISTS files(id INTEGER PRIMARY KEY, scan TEXT, path TEXT, identity TEXT, length INTEGER, record TEXT, hash TEXT, sample TEXT, group_id INTEGER);
CREATE UNIQUE INDEX IF NOT EXISTS physical ON files(scan,identity);
CREATE INDEX IF NOT EXISTS sizes ON files(scan,length);
CREATE INDEX IF NOT EXISTS hashes ON files(scan,length,hash);
CREATE INDEX IF NOT EXISTS groups ON files(scan,group_id);
CREATE TABLE IF NOT EXISTS issues(id INTEGER PRIMARY KEY, scan TEXT, path TEXT, reason TEXT);
CREATE TABLE IF NOT EXISTS journal(id TEXT PRIMARY KEY, plan TEXT, source TEXT, destination TEXT, action TEXT, state TEXT, detail TEXT, record TEXT, created TEXT, hash TEXT);

ALTER TABLE journal ADD COLUMN keeper_record TEXT;
ALTER TABLE journal ADD COLUMN recycle_receipt TEXT;
UPDATE journal SET hash='' WHERE hash IS NULL;
CREATE TABLE scan_metrics(
    scan TEXT NOT NULL REFERENCES scans(id),
    phase TEXT NOT NULL,
    elapsed_ms INTEGER NOT NULL CHECK(elapsed_ms >= 0),
    PRIMARY KEY(scan, phase)
);
CREATE INDEX sample_candidates ON files(scan,length,sample);
```
