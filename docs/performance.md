# Reproducible measurement

```powershell
./scripts/New-BenchmarkDataset.ps1 -Destination benchmark-data -Files 10000 -BytesPerFile 65536 -Seed 7301
./scripts/build.ps1 -Package
```

Scan the generated directory with default settings and record storage type, filesystem, profile, SDK/runtime, wall time, discovered/hashed counts, displayed byte counts, issues, group count, private bytes and handle count from Windows Task Manager. Repeat cold and warm cache runs separately. Never use real user files to test cleanup. Generated data is excluded from source control and the generator refuses an existing output directory.

Increase file counts to test metadata scaling, and increase bytes per file to test stream memory bounds. A single large duplicate bucket must not increase scanner buffer memory with file length: SHA-256 and byte verification use bounded streams. SQLite database and filesystem caches consume additional memory; the bounded UI caps result pages at 25 groups and 500 displayed members per group. Exports stream rows, with no group-sized collection.

Enumeration, sampling, hashing, byte verification and finalization accumulate milliseconds in scan_metrics. Database-write time overlaps those phases and must not be added to them. Metadata inserts commit at most 256 records per transaction; hashes, group updates and journal transitions still use individual FULL-sync mutations. The 2,002-file regression writes artifacts/performance/metadata-smoke.json with phase and wall timings, under the test process with warm generated files; it does not establish isolated memory or cold-disk throughput. No million-file performance claim has been established. Use Windows Performance Recorder for disk throughput, SQLite/persistence CPU and UI responsiveness measurements before tuning or introducing bounded worker concurrency.
