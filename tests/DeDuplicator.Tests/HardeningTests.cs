using System.IO;
using System.Text.Json;
using DeDuplicator.Core;
using DeDuplicator.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeDuplicator.Tests;

public sealed class HardeningTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "DeDuplicatorHardening", Guid.NewGuid().ToString("N"));
    private string Input => Path.Combine(root, "input");
    public HardeningTests() => Directory.CreateDirectory(Input);
    private string Write(string name, string text) { string path = Path.Combine(Input, name); File.WriteAllText(path, text); return path; }
    private ScanStore Store() => new(Path.Combine(root, "scans.db"));
    private async Task<(ScanStore Store, string Scan, ActionPlan Plan)> Fixture(CleanupAction action = CleanupAction.Quarantine)
    {
        Write("a", "verified contents"); Write("b", "verified contents"); var store = Store();
        string scan = await new Scanner(store, new Sha256Hasher()).ScanAsync(new() { Roots = [Input] }, null, default);
        return (store, scan, Planner.Create(store.Groups(scan), new(), KeepRule.ShortestPath, action, Path.Combine(root, "quarantine")));
    }

    [Fact] public void ProfileRejectsNullListsAndInvalidSizesBeforeScanning()
    {
        Assert.Throws<ArgumentException>(() => ProfileValidation.Validate(new() { Roots = null! }));
        Assert.Throws<ArgumentException>(() => ProfileValidation.Validate(new() { Roots = [Input], MinimumSize = 100, MaximumSize = 10 }));
        Assert.Throws<ArgumentException>(() => ProfileValidation.Validate(new() { Roots = [Input], IncludedExtensions = ["*.txt"] }));
        Assert.Throws<ArgumentException>(() => ProfileValidation.Validate(new()));
        Assert.Equal(".pdf", ProfileValidation.Validate(new() { Roots = [Input], IncludedExtensions = ["pdf"] }).IncludedExtensions[0]);
    }

    [Fact] public async Task CancelledAndFailedExportsNeverPublishAPartialFinalFile()
    {
        var (store, scan, _) = await Fixture(); string target = Path.Combine(root, "export.json");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ResultExporter.ExportAsync(store, scan, target, true, cancelled.Token)); Assert.False(File.Exists(target));
        store.Execute("UPDATE files SET record='{invalid json' WHERE scan=$scan", ("$scan", scan));
        await Assert.ThrowsAsync<JsonException>(() => ResultExporter.ExportAsync(store, scan, target, true, default));
        Assert.False(File.Exists(target)); Assert.Empty(Directory.GetFiles(root, "*.deduplicator-export.tmp"));
    }

    [Fact] public async Task ExportNeverOverwritesAnExistingDestination()
    {
        var (store, scan, _) = await Fixture(); string target = Path.Combine(root, "export.json"); File.WriteAllText(target, "keep this");
        await Assert.ThrowsAsync<IOException>(() => ResultExporter.ExportAsync(store, scan, target, true, default)); Assert.Equal("keep this", File.ReadAllText(target));
    }

    private sealed class CopyLockProbe(ScanStore store) : CleanupService(store)
    {
        private int renames;
        public bool Probed { get; private set; }
        protected override void Rename(NativeFile source, string destination)
        {
            if (++renames == 1) throw new System.ComponentModel.Win32Exception(17);
            base.Rename(source, destination);
        }
        protected override void RemoveCopiedSource(NativeFile source, string destination)
        {
            Assert.Throws<IOException>(() => File.WriteAllText(destination, "tamper"));
            Assert.Throws<IOException>(() => File.Delete(destination));
            Probed = true; base.RemoveCopiedSource(source, destination);
        }
    }

    [Fact] public async Task CrossVolumeDestinationStaysLockedThroughSourceRemoval()
    {
        var (store, _, plan) = await Fixture(); var cleanup = new CopyLockProbe(store);
        var result = await cleanup.ExecuteAsync(plan, false, null, default);
        Assert.StartsWith("Complete:", result[0]); Assert.True(cleanup.Probed);
        Assert.False(File.Exists(plan.Files[0].Source.Path)); Assert.Equal("verified contents", File.ReadAllText(plan.Files[0].Destination!));
    }

    private sealed class UnavailableRecycler : IFileRecycler
    {
        public Task<RecycleReceipt> RecycleAsync(FileRecord staged, NativeFile keeper, CancellationToken token) => throw new IOException("Injected unsupported recycle location.");
        public Task RestoreAsync(RecycleReceipt receipt, string destination, string hash, long length, CancellationToken token) => throw new IOException("No recycle receipt was created.");
    }

    [Fact] public async Task UnavailableRecycleRetainsVerifiedStagingAndCanRestoreWithoutDeletionFallback()
    {
        var (store, _, plan) = await Fixture(CleanupAction.Recycle); var cleanup = new CleanupService(store, new UnavailableRecycler());
        await cleanup.ExecuteAsync(plan, false, null, default); var entry = Assert.Single(cleanup.Journal());
        Assert.Equal("Recycle retained", entry.State); Assert.False(File.Exists(entry.Source)); Assert.Equal("verified contents", File.ReadAllText(entry.Destination!));
        await cleanup.RestoreAsync(entry, default); Assert.Equal("verified contents", File.ReadAllText(entry.Source)); Assert.True(File.Exists(plan.Files[0].Keeper.Path));
    }

    [Fact] [Trait("WindowsFeature", "Shell Recycle Bin")]
    public async Task RealRecycleAndJournalRestorePreserveContentsAndRefuseConflicts()
    {
        var (store, _, plan) = await Fixture(CleanupAction.Recycle); var cleanup = new CleanupService(store);
        var result = await cleanup.ExecuteAsync(plan, false, null, default); var entry = Assert.Single(cleanup.Journal());
        Assert.True(result[0].StartsWith("Complete:"), result[0]); Assert.NotNull(entry.Recycle); Assert.False(File.Exists(entry.Source)); Assert.True(File.Exists(entry.Recycle.FilePath));
        try
        {
            File.WriteAllText(entry.Source, "occupied"); await Assert.ThrowsAsync<IOException>(() => cleanup.RestoreAsync(entry, default)); Assert.Equal("occupied", File.ReadAllText(entry.Source));
        }
        finally { if (File.Exists(entry.Source)) File.Delete(entry.Source); await cleanup.RestoreAsync(entry, default); }
        Assert.Equal("verified contents", File.ReadAllText(entry.Source)); Assert.False(File.Exists(entry.Recycle.FilePath)); Assert.True(File.Exists(plan.Files[0].Keeper.Path));
    }

    [Fact] public void LegacyDatabaseUpgradePreservesJournalAndRejectsFutureSchema()
    {
        string path = Path.Combine(root, "legacy.db");
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString()))
        {
            db.Open(); using var command = db.CreateCommand();
            using var stream = typeof(SchemaMigrator).Assembly.GetManifestResourceStream("DeDuplicator.Infrastructure.Migrations.001_initial.sql")!;
            using var text = new StreamReader(stream); command.CommandText = text.ReadToEnd(); command.ExecuteNonQuery();
            command.CommandText = "INSERT INTO journal(id,source,action,state,record,hash) VALUES('legacy','original','Quarantine','Complete','{}','')"; command.ExecuteNonQuery();
        }
        var store = new ScanStore(path);
        using (var db = store.Open())
        {
            using var command = db.CreateCommand(); command.CommandText = "PRAGMA user_version"; Assert.Equal(SchemaMigrator.CurrentVersion, Convert.ToInt32(command.ExecuteScalar()));
            command.CommandText = "SELECT count(*) FROM journal WHERE id='legacy'"; Assert.Equal(1L, command.ExecuteScalar());
            command.CommandText = "PRAGMA user_version=999"; command.ExecuteNonQuery();
        }
        Assert.Throws<InvalidDataException>(() => new ScanStore(path));
        using var original = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString()); original.Open();
        using var verify = original.CreateCommand(); verify.CommandText = "SELECT count(*) FROM journal WHERE id='legacy'"; Assert.Equal(1L, verify.ExecuteScalar());
    }

    [Fact] public void UnknownLegacySchemaFailsWithoutStampingOrRemovingData()
    {
        string path = Path.Combine(root, "unknown.db");
        using var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString()); db.Open();
        using var command = db.CreateCommand(); command.CommandText = "CREATE TABLE journal(important TEXT); INSERT INTO journal VALUES('recovery data')"; command.ExecuteNonQuery();
        Assert.Throws<InvalidDataException>(() => new ScanStore(path));
        command.CommandText = "PRAGMA user_version"; Assert.Equal(0L, command.ExecuteScalar());
        command.CommandText = "SELECT important FROM journal"; Assert.Equal("recovery data", command.ExecuteScalar());
    }

    [Fact] public void FailedMigrationRollsBackAllNewColumnsAndVersion()
    {
        string path = Path.Combine(root, "rollback.db");
        using var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString()); db.Open();
        using var command = db.CreateCommand();
        using var stream = typeof(SchemaMigrator).Assembly.GetManifestResourceStream("DeDuplicator.Infrastructure.Migrations.001_initial.sql")!;
        using var reader = new StreamReader(stream); command.CommandText = reader.ReadToEnd(); command.ExecuteNonQuery();
        command.CommandText = "ALTER TABLE journal ADD COLUMN recycle_receipt TEXT; PRAGMA user_version=1"; command.ExecuteNonQuery();
        Assert.Throws<SqliteException>(() => new ScanStore(path));
        command.CommandText = "PRAGMA user_version"; Assert.Equal(1L, command.ExecuteScalar());
        command.CommandText = "SELECT count(*) FROM pragma_table_info('journal') WHERE name='keeper_record'"; Assert.Equal(0L, command.ExecuteScalar());
    }

    [Fact] public async Task MetadataBatchesAndPhaseMetricsPreserveAllRows()
    {
        for (int i = 1; i <= 2000; i++) { using var file = File.Create(Path.Combine(Input, $"metadata-{i}")); file.SetLength(i); }
        Write("duplicate-a", new string('x', 4096)); Write("duplicate-b", new string('x', 4096));
        var store = Store(); var timer = System.Diagnostics.Stopwatch.StartNew();
        string scan = await new Scanner(store, new Sha256Hasher()).ScanAsync(new() { Roots = [Input] }, null, default);
        using var db = store.Open(); using var command = db.CreateCommand();
        command.CommandText = "SELECT count(*) FROM files"; Assert.Equal(2002L, command.ExecuteScalar());
        Assert.Single(store.Groups(scan));
        command.CommandText = "SELECT phase,elapsed_ms FROM scan_metrics ORDER BY phase";
        var metrics = new Dictionary<string, long>(); using (var values = command.ExecuteReader()) while (values.Read()) metrics.Add(values.GetString(0), values.GetInt64(1));
        Assert.Contains("Enumerating", metrics.Keys); Assert.Contains("Hashing", metrics.Keys); Assert.Contains("Byte verification", metrics.Keys);
        Assert.All(metrics.Values, value => Assert.True(value >= 0));
        string? repository = AppContext.BaseDirectory;
        while (repository != null && !File.Exists(Path.Combine(repository, "Version.props"))) repository = Path.GetDirectoryName(repository);
        Assert.NotNull(repository); Directory.CreateDirectory(Path.Combine(repository, "artifacts", "performance"));
        File.WriteAllText(Path.Combine(repository, "artifacts", "performance", "metadata-smoke.json"), JsonSerializer.Serialize(new { Files = 2002, WallMilliseconds = timer.ElapsedMilliseconds, Metrics = metrics, Environment = System.Runtime.InteropServices.RuntimeInformation.OSDescription, Note = "Generated temporary local files; warm cache; in-process regression, not a million-file or isolated memory benchmark." }, new JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact] public void ApplicationDataLeaseExcludesConcurrentRecoveryAndReopensAfterExit()
    {
        using (var first = new ApplicationDataLease(root))
            Assert.Throws<IOException>(() => new ApplicationDataLease(root));
        using var next = new ApplicationDataLease(root);
        Assert.True(File.Exists(Path.Combine(root, ".instance.lock")));
    }

    [Fact] public void EveryConnectionEnforcesFullSynchronousAndForeignKeys()
    {
        var store = Store(); using var db = store.Open(); using var command = db.CreateCommand();
        command.CommandText = "PRAGMA synchronous"; Assert.Equal(2L, command.ExecuteScalar());
        command.CommandText = "PRAGMA foreign_keys"; Assert.Equal(1L, command.ExecuteScalar());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        string prefix = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "DeDuplicatorHardening")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(root).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new IOException("Unsafe fixture cleanup path.");
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
