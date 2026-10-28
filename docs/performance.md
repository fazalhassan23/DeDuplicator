# Reproducible measurement

```powershell
./scripts/New-BenchmarkDataset.ps1 -Destination benchmark-data -Files 10000 -BytesPerFile 65536 -Seed 7301
./scripts/build.ps1 -Package
```

Scan the generated directory with default settings and record storage type, filesystem, profile, SDK/runtime, wall time, discovered/hashed counts, displayed byte counts, issues, group count, private bytes and handle count from Windows Task Manager. Repeat cold and warm cache runs separately. Never use real user files to test cleanup. Generated data is excluded from source control and the generator refuses an existing output directory.

Increase file counts to test metadata scaling, and increase bytes per file to test stream memory bounds. A single large duplicate bucket must not increase scanner buffer memory with file length: SHA-256 and byte verification use bounded streams. SQLite database and filesystem caches consume additional memory; the bounded UI caps result pages at 25 groups and 500 displayed members per group. Exports stream rows, with no group-sized collection.

Enumeration, sampling, hashing and verification are separate visible phases. Record transition wall times for now; separate persisted phase timings are not implemented. Per-file FULL-sync SQLite commits prioritize crash visibility over throughput and are expected to limit million-file enumeration speed. No million-file performance claim has been established. Use Windows Performance Recorder for disk throughput, SQLite/persistence CPU and UI responsiveness measurements before tuning or introducing bounded worker concurrency.
