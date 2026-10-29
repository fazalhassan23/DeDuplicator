# Generated persistent entities

Generated from the same ordered migrations as the [structural reference](schema-reference.md).
Relationship labels distinguish logical links from enforced foreign keys.

```mermaid
erDiagram
    files {
        INTEGER id PK
        TEXT scan
        TEXT path
        TEXT identity
        INTEGER length
        TEXT record
        TEXT hash
        TEXT sample
        INTEGER group_id
    }
    issues {
        INTEGER id PK
        TEXT scan
        TEXT path
        TEXT reason
    }
    journal {
        TEXT id PK
        TEXT plan
        TEXT source
        TEXT destination
        TEXT action
        TEXT state
        TEXT detail
        TEXT record
        TEXT created
        TEXT hash
        TEXT keeper_record
        TEXT recycle_receipt
    }
    scan_metrics {
        TEXT scan PK
        TEXT phase PK
        INTEGER elapsed_ms
    }
    scans ||--o{ scan_metrics : "FK scan"
    scans {
        TEXT id PK
        TEXT started
        TEXT state
        TEXT profile
    }
    scans ||--o{ files : "logical scan id"
    scans ||--o{ issues : "logical scan id"
```

`files.group_id` is a provisional representative file-row ID, without a foreign key or group table. `journal.plan` is an action-plan UUID without a persisted plan table. Journal snapshots intentionally survive independently of scan rows.
