using System.Runtime.InteropServices;
using System.IO;
using DeDuplicator.Core;
using DeDuplicator.Infrastructure;
using Xunit;

namespace DeDuplicator.Tests;

public sealed class SafetyTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "DeDuplicatorTests", Guid.NewGuid().ToString("N"));
    private readonly ScanStore store;
    private string Input => Path.Combine(root, "input");
    public SafetyTests() { Directory.CreateDirectory(Input); store = new(Path.Combine(root, "state.db")); }
    private string Write(string name, string content)
    {
        string path = Path.Combine(Input, name); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, content); return path;
    }
    private async Task<IReadOnlyList<DuplicateGroup>> Run(ScanProfile? profile = null, IContentHasher? hash = null)
    {
        var scanner = new Scanner(store, hash ?? new Sha256Hasher());
        string id = await scanner.ScanAsync(profile ?? new() { Roots = [Input] }, null, default); return store.Groups(id);
    }
    [Fact] public async Task DifferentNamesAreDuplicatesAndSameNamesDoNotProveEquality()
    {
        Write("a/report.txt", "same"); Write("b/holiday.dat", "same"); Write("b/report.txt", "else");
        var groups = await Run(); Assert.Single(groups); Assert.Equal(2, groups[0].Files.Count);
    }
    private sealed class CollisionHasher : IContentHasher { public Task<string> HashAsync(Stream stream, CancellationToken token) => Task.FromResult("forced collision"); }
    [Fact] public async Task ForcedHashCollisionStillRequiresByteEquality()
    {
        Write("a", "aaaa"); Write("b", "bbbb"); Write("c", "aaaa"); Write("d", "bbbb");
        var groups = await Run(hash: new CollisionHasher()); Assert.Equal(2, groups.Count); Assert.All(groups, g => Assert.Equal(2, g.Files.Count));
    }
    [Fact] public async Task MatchingSamplesDoNotHideDifferentUnsampledBytes()
    {
        byte[] data = new byte[2 * 1024 * 1024]; File.WriteAllBytes(Path.Combine(Input, "a"), data); data[100000] = 1; File.WriteAllBytes(Path.Combine(Input, "b"), data);
        Assert.Empty(await Run()); Assert.Empty(await Run(hash: new CollisionHasher()));
    }
    [Fact] public async Task EmptyFilesAreExplicitlyOptIn()
    {
        Write("a", ""); Write("b", ""); Assert.Empty(await Run());
        Assert.Single(await Run(new() { Roots = [Input], IncludeEmpty = true }));
    }
    [Fact] public async Task UnicodeLongPathsAndOverlappingRoots()
    {
        string nested = string.Join('/', Enumerable.Repeat("long-directory-名前", 16));
        Write(nested + "/写真.txt", "abc"); Write("copy", "abc");
        var groups = await Run(new() { Roots = [Input, Path.Combine(Input, nested), Input] }); Assert.Single(groups); Assert.Equal(2, groups[0].Files.Count);
    }
    [Fact] public async Task HardLinksDoNotCountAsIndependentCopies()
    {
        string a = Write("original", "same"); string alias = Path.Combine(Input, "alias");
        Assert.True(CreateHardLinkW(alias, a, IntPtr.Zero), "NTFS hard-link fixture required.");
        Assert.Empty(await Run()); Write("independent", "same"); var group = Assert.Single(await Run()); Assert.Equal(2, group.Files.Count); Assert.Contains(group.Files, f => f.Links == 2 && f.ReviewRequired);
    }
    [Fact] public async Task LockedFilesAndMissingRootsAreRecordedWithoutCrashing()
    {
        string a = Write("locked", "same"); Write("other", "same");
        using var file = new FileStream(a, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var scanner = new Scanner(store, new Sha256Hasher()); string id = await scanner.ScanAsync(new() { Roots = [Input, Path.Combine(root, "disconnected")] }, null, default);
        Assert.Empty(store.Groups(id)); Assert.NotEmpty(store.Diagnostics(id));
    }
    [Fact] public async Task CancellationPersistsAnHonestState()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new Scanner(store, new Sha256Hasher()).ScanAsync(new() { Roots = [Input] }, null, cancellation.Token));
        Assert.Contains("Cancelled", store.History()[0].State);
    }
    private sealed class MutationHasher(string path) : IContentHasher
    {
        public bool WriteWasDenied { get; private set; }
        public async Task<string> HashAsync(Stream stream, CancellationToken token)
        {
            try { File.WriteAllText(path, "changed"); } catch (IOException) { WriteWasDenied = true; }
            return await new Sha256Hasher().HashAsync(stream, token);
        }
    }
    [Fact] public async Task ConcurrentMutationIsDeniedWhileHashing()
    {
        string a = Write("a", "same"); Write("b", "same"); var hash = new MutationHasher(a); await Run(hash: hash); Assert.True(hash.WriteWasDenied);
    }
    [Fact] public async Task QuarantineRestoreAndRestoreConflictNeverOverwrite()
    {
        Write("a", "same"); Write("b", "same"); var groups = await Run(); var profile = new ScanProfile { Roots = [Input] };
        var plan = Planner.Create(groups, profile, KeepRule.ShortestPath, CleanupAction.Quarantine, Path.Combine(root, "quarantine"));
        var cleanup = new CleanupService(store); var result = await cleanup.ExecuteAsync(plan, false, null, default);
        Assert.StartsWith("Complete:", Assert.Single(result)); var entry = Assert.Single(cleanup.Journal()); Assert.False(File.Exists(entry.Source)); Assert.True(File.Exists(entry.Destination));
        File.WriteAllText(entry.Source, "occupant"); await Assert.ThrowsAsync<IOException>(() => cleanup.RestoreAsync(entry, default)); Assert.Equal("occupant", File.ReadAllText(entry.Source));
        File.Delete(entry.Source); await cleanup.RestoreAsync(entry, default); Assert.True(File.Exists(entry.Source), string.Join(" | ", Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))); Assert.Equal("same", File.ReadAllText(entry.Source));
    }
    [Fact] public async Task ChangedOrMissingKeeperPreventsCleanup()
    {
        Write("a", "same"); Write("b", "same"); var groups = await Run(); var plan = Planner.Create(groups, new(), KeepRule.ShortestPath, CleanupAction.Quarantine, Path.Combine(root, "q"));
        File.WriteAllText(plan.Files[0].Keeper.Path, "else"); var result = await new CleanupService(store).ExecuteAsync(plan, false, null, default); Assert.StartsWith("Skipped:", result[0]); Assert.True(File.Exists(plan.Files[0].Source.Path));
        File.Delete(plan.Files[0].Keeper.Path); result = await new CleanupService(store).ExecuteAsync(plan, false, null, default); Assert.StartsWith("Skipped:", result[0]);
    }
    [Fact] public async Task DestinationCollisionAndPartialFailureAreJournaled()
    {
        Write("a", "same"); Write("b", "same"); Write("c", "same"); var groups = await Run(); var plan = Planner.Create(groups, new(), KeepRule.ShortestPath, CleanupAction.Quarantine, Path.Combine(root, "q"));
        Directory.CreateDirectory(Path.GetDirectoryName(plan.Files[0].Destination!)!); File.WriteAllText(plan.Files[0].Destination!, "occupied");
        var cleanup = new CleanupService(store); var results = await cleanup.ExecuteAsync(plan, false, null, default);
        Assert.StartsWith("Skipped:", results[0]); Assert.True(results[1].StartsWith("Complete:"), results[1]); Assert.Equal("occupied", File.ReadAllText(plan.Files[0].Destination!)); Assert.Equal(2, cleanup.Journal().Count);
    }
    [Fact] public async Task ProtectedFilesAndLastKeeperAreRetained()
    {
        Write("a", "same"); Write("protected/b", "same"); var groups = await Run(); var profile = new ScanProfile { ProtectedDirectories = [Path.Combine(Input, "protected")] };
        var plan = Planner.Create(groups, profile, KeepRule.Newest, CleanupAction.Quarantine, Path.Combine(root, "q"));
        Assert.Single(plan.Files); Assert.Contains("protected", plan.Files[0].Keeper.Path); Assert.DoesNotContain("protected", plan.Files[0].Source.Path);
    }
    [Fact] public async Task ExtraStreamsRequireReview()
    {
        string a = Write("a", "same"); Write("b", "same"); File.WriteAllText(a + ":information", "extra");
        var group = Assert.Single(await Run()); Assert.True(group.Files.Single(f => f.Path == a).ReviewRequired);
    }
    [Fact] public async Task PermanentDeletionRequiresExplicitOptIn()
    {
        Write("a", "same"); Write("b", "same"); var groups = await Run(); var cleanup = new CleanupService(store);
        foreach (var action in new[] { CleanupAction.PermanentDelete })
        { var plan = Planner.Create(groups, new(), KeepRule.ShortestPath, action, Path.Combine(root, "q")); var results = await cleanup.ExecuteAsync(plan, false, null, default); Assert.StartsWith("Skipped:", results[0]); Assert.True(File.Exists(plan.Files[0].Source.Path)); }
    }
    [Fact] public async Task HistoricalRunningScansAndOperationsBecomeInterrupted()
    {
        store.Start("interrupted", new()); store.Recover(); Assert.Contains("Interrupted", store.History()[0].State);
        Write("a", "same"); Write("b", "same"); await Run(); store.Recover(); Assert.Contains(store.History(), s => s.State == "Complete");
    }
    private sealed class CrossVolumeCleanup(ScanStore store, bool interrupt = false) : CleanupService(store)
    {
        private int renames;
        protected override void Rename(NativeFile file, string destination)
        {
            if (++renames == 1) throw new System.ComponentModel.Win32Exception(17);
            if (interrupt) throw new OperationCanceledException("Injected interruption after durable copy.");
            base.Rename(file, destination);
        }
    }
    [Fact] public async Task CrossVolumeCopyIsVerifiedBeforeSourceRemoval()
    {
        Write("a", "same"); Write("b", "same"); var plan = Planner.Create(await Run(), new(), KeepRule.ShortestPath, CleanupAction.Quarantine, Path.Combine(root, "q"));
        var result = await new CrossVolumeCleanup(store).ExecuteAsync(plan, false, null, default);
        Assert.StartsWith("Complete:", result[0]); Assert.False(File.Exists(plan.Files[0].Source.Path)); Assert.Equal("same", File.ReadAllText(plan.Files[0].Destination!));
    }
    [Fact] public async Task InterruptedCrossVolumeCopyRetainsSourceAndJournal()
    {
        Write("a", "same"); Write("b", "same"); var plan = Planner.Create(await Run(), new(), KeepRule.ShortestPath, CleanupAction.Quarantine, Path.Combine(root, "q"));
        var cleanup = new CrossVolumeCleanup(store, true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cleanup.ExecuteAsync(plan, false, null, default));
        Assert.Equal("same", File.ReadAllText(plan.Files[0].Source.Path)); Assert.Equal("same", File.ReadAllText(plan.Files[0].Destination + ".partial")); Assert.Equal("Interrupted", Assert.Single(cleanup.Journal()).State);
    }
    [Fact] public async Task KeeperScheduledForRemovalIsRejectedBeforeAnyAction()
    {
        Write("a", "same"); Write("b", "same"); var group = Assert.Single(await Run());
        var plan = new ActionPlan(Guid.NewGuid(), DateTime.UtcNow, [new(group.Files[0], group.Files[1], CleanupAction.PermanentDelete, null, "invalid"), new(group.Files[1], group.Files[0], CleanupAction.PermanentDelete, null, "invalid")], new());
        await Assert.ThrowsAsync<IOException>(() => new CleanupService(store).ExecuteAsync(plan, true, null, default)); Assert.All(group.Files, f => Assert.True(File.Exists(f.Path))); Assert.Empty(new CleanupService(store).Journal());
    }
    [Fact] public async Task RestoreRefusesChangedQuarantineContents()
    {
        Write("a", "same"); Write("b", "same"); var plan = Planner.Create(await Run(), new(), KeepRule.ShortestPath, CleanupAction.Quarantine, Path.Combine(root, "q"));
        var cleanup = new CleanupService(store); await cleanup.ExecuteAsync(plan, false, null, default); var entry = Assert.Single(cleanup.Journal());
        File.WriteAllText(entry.Destination!, "else"); await Assert.ThrowsAsync<IOException>(() => cleanup.RestoreAsync(entry, default)); Assert.False(File.Exists(entry.Source));
    }
    [Fact] [Trait("WindowsFeature", "NTFS junctions")]
    public async Task JunctionCycleIsSkipped()
    {
        Write("a", "same"); Write("b", "same"); string junction = Path.Combine(Input, "cycle");
        var start = new System.Diagnostics.ProcessStartInfo("cmd.exe") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("/c"); start.ArgumentList.Add("mklink"); start.ArgumentList.Add("/J"); start.ArgumentList.Add(junction); start.ArgumentList.Add(Input);
        using var process = System.Diagnostics.Process.Start(start)!; await process.WaitForExitAsync(); Assert.Equal(0, process.ExitCode);
        try { Assert.Single(await Run()); } finally { Directory.Delete(junction); }
    }
    [Fact] public async Task StreamingExportIncludesAllGroupsAndEscapesNames()
    {
        Write("=formula", "same"); Write("b", "same"); string scan = await new Scanner(store, new Sha256Hasher()).ScanAsync(new() { Roots = [Input] }, null, default);
        string json = Path.Combine(root, "results.json"), csv = Path.Combine(root, "results.csv");
        await ResultExporter.ExportAsync(store, scan, json, true, default); await ResultExporter.ExportAsync(store, scan, csv, false, default);
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(json)); Assert.Equal(2, document.RootElement.GetArrayLength()); Assert.Equal(3, File.ReadAllLines(csv).Length);
    }
    [Fact] public async Task LargeFilesAreComparedWithBoundedBuffers()
    {
        foreach (string name in new[] { "large-a", "large-b" })
        { await using var output = new FileStream(Path.Combine(Input, name), FileMode.CreateNew); byte[] chunk = new byte[65536]; for (int i = 0; i < 512; i++) await output.WriteAsync(chunk); }
        Assert.Single(await Run());
    }
    [Fact] public async Task LargeGroupsPageWithoutLosingExportedMembersAndRetainASubsetKeeper()
    {
        for (int i = 0; i < 501; i++) Write($"copy-{i:D4}", "same");
        string scan = await new Scanner(store, new Sha256Hasher()).ScanAsync(new() { Roots = [Input] }, null, default);
        var group = Assert.Single(store.Groups(scan)); Assert.True(group.Partial); Assert.Equal(500, group.Files.Count);
        Assert.Single(store.GroupMembers(scan, group.Id, 500).Files);
        var plan = Planner.Create([group], new(), KeepRule.ShortestPath, CleanupAction.Quarantine, Path.Combine(root, "q")); Assert.Equal(499, plan.Files.Count);
        Assert.DoesNotContain(plan.Files, f => plan.Files.Any(other => f.Keeper.Identity == other.Source.Identity));
        string json = Path.Combine(root, "all-copies.json"); await ResultExporter.ExportAsync(store, scan, json, true, default);
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(json)); Assert.Equal(501, document.RootElement.GetArrayLength());
    }
    [Fact] public void InterruptedProvisionalSingletonNeverAppearsAsVerifiedDuplicate()
    {
        string path = Write("only", "same"); store.Start("partial", new()); using (var file = NativeFile.Open(path)) store.Add("partial", file.Record);
        store.Execute("UPDATE files SET hash='candidate',group_id=id WHERE scan='partial'"); store.Recover(); Assert.Empty(store.Groups("partial"));
    }
    [Theory] [InlineData("=cmd()")] [InlineData("+SUM(1)")] [InlineData("-2")] [InlineData("@evil")] [InlineData("\tformula")] [InlineData("  =x")]
    public void CsvPreventsSpreadsheetFormulaInjection(string value) => Assert.StartsWith("\"'", ResultExporter.CsvCell(value));
    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (!Path.GetFullPath(root).StartsWith(Path.Combine(Path.GetTempPath(), "DeDuplicatorTests"), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsafe fixture path.");
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CreateHardLinkW(string link, string existing, IntPtr security);
}
