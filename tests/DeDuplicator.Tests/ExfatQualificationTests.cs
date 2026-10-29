using System.IO;
using System.Security.Cryptography;
using DeDuplicator.Core;
using DeDuplicator.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeDuplicator.Tests;

public sealed class ExfatVolumeFactAttribute : FactAttribute
{
    public ExfatVolumeFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEDUPLICATOR_EXFAT_ROOT")))
            Skip = "Real exFAT qualification requires DEDUPLICATOR_EXFAT_ROOT pointing to an existing exFAT volume; no volume is formatted.";
    }
}

public sealed class ExfatQualificationTests
{
    [ExfatVolumeFact]
    [Trait("WindowsFeature", "Real exFAT volume")]
    public async Task RealExfatScanQuarantineCrossVolumeRestoreAndPermanentOptIn()
    {
        string volume = Path.GetFullPath(Environment.GetEnvironmentVariable("DEDUPLICATOR_EXFAT_ROOT")!);
        Assert.True(Directory.Exists(volume), "Supply an existing exFAT directory; never format a volume for this test.");
        string name = "DeDuplicator-qualification-" + Guid.NewGuid().ToString("N");
        string root = Path.Combine(volume, name), local = Path.Combine(Path.GetTempPath(), name);
        Directory.CreateDirectory(root); Directory.CreateDirectory(local);
        try
        {
            string input = Path.Combine(root, "input"); Directory.CreateDirectory(input);
            byte[] content = new byte[1024 * 1024 + 17]; new Random(7301).NextBytes(content);
            string first = Path.Combine(input, "original-α.txt"), second = Path.Combine(input, "different-name.txt");
            await File.WriteAllBytesAsync(first, content); await File.WriteAllBytesAsync(second, content);
            await File.WriteAllTextAsync(Path.Combine(input, "unique.txt"), "unique generated contents");
            using (var file = NativeFile.Open(first)) { Assert.Equal("exFAT", file.FileSystemName, ignoreCase: true); Assert.False(file.Record.ReviewRequired); }
            string probe = Path.Combine(local, "probe"); await File.WriteAllTextAsync(probe, "local probe");
            using (var file = NativeFile.Open(probe)) Assert.Equal("NTFS", file.FileSystemName, ignoreCase: true);
            var store = new ScanStore(Path.Combine(local, "scans.db")); var cleanup = new CleanupService(store);
            var profile = new ScanProfile { Roots = [input] };
            async Task<ActionPlan> Plan(CleanupAction action, string destination)
            {
                string scan = await new Scanner(store, new Sha256Hasher()).ScanAsync(profile, null, default);
                var group = Assert.Single(store.Groups(scan)); Assert.Equal(2, group.Files.Count);
                Assert.Equal(2, group.Files.Select(file => file.Identity).Distinct().Count());
                Assert.All(group.Files, file => Assert.False(file.ReviewRequired));
                return Planner.Create([group], profile, KeepRule.ShortestPath, action, destination);
            }
            foreach (string destination in new[] { Path.Combine(root, "quarantine"), Path.Combine(local, "quarantine") })
            {
                var plan = await Plan(CleanupAction.Quarantine, destination); var operation = Assert.Single(plan.Files);
                var outcome = await cleanup.ExecuteAsync(plan, false, null, default); Assert.StartsWith("Complete:", Assert.Single(outcome));
                Assert.False(File.Exists(operation.Source.Path)); Assert.True(File.Exists(operation.Keeper.Path));
                Assert.Equal(content, await File.ReadAllBytesAsync(operation.Destination!));
                var entry = cleanup.Journal().First(); await cleanup.RestoreAsync(entry, default);
                Assert.Equal(content, await File.ReadAllBytesAsync(operation.Source.Path)); Assert.False(File.Exists(operation.Destination!));
            }
            var recycling = await Plan(CleanupAction.Recycle, Path.Combine(root, "recycle-staging"));
            await cleanup.ExecuteAsync(recycling, false, null, default); var recycled = cleanup.Journal().First();
            Assert.True(recycled.State is "Complete" or "Recycle retained", recycled.State + ": " + recycled.Detail);
            await cleanup.RestoreAsync(recycled, default); Assert.Equal(content, await File.ReadAllBytesAsync(recycled.Source));
            var deletion = await Plan(CleanupAction.PermanentDelete, "unused"); var removed = Assert.Single(deletion.Files);
            await cleanup.ExecuteAsync(deletion, false, null, default); Assert.True(File.Exists(removed.Source.Path));
            var deleted = await cleanup.ExecuteAsync(deletion, true, null, default); Assert.StartsWith("Complete:", Assert.Single(deleted));
            Assert.False(File.Exists(removed.Source.Path)); Assert.Equal(content, await File.ReadAllBytesAsync(removed.Keeper.Path));
            string? repository = AppContext.BaseDirectory;
            while (repository != null && !File.Exists(Path.Combine(repository, "Version.props"))) repository = Path.GetDirectoryName(repository);
            Assert.NotNull(repository); Directory.CreateDirectory(Path.Combine(repository, "artifacts", "qualification"));
            await File.WriteAllTextAsync(Path.Combine(repository, "artifacts", "qualification", "exfat.txt"), $"Real exFAT source; NTFS database and cross-volume destination; same-volume and cross-volume quarantine/restore passed; recycle outcome: {recycled.State}; permanent deletion refused until explicit opt-in; keeper SHA-256 {Convert.ToHexString(SHA256.HashData(content))}.\n");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            RemoveOwnedFixture(root, volume, name); RemoveOwnedFixture(local, Path.GetTempPath(), name);
        }
    }

    private static void RemoveOwnedFixture(string path, string parent, string name)
    {
        if (Path.GetFullPath(path) != Path.GetFullPath(Path.Combine(parent, name)) || !name.StartsWith("DeDuplicator-qualification-", StringComparison.Ordinal)) throw new IOException("Unsafe fixture cleanup path.");
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }
}
