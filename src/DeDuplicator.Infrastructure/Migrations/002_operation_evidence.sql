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
