using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using DeDuplicator.Core;

namespace DeDuplicator.Infrastructure;

public interface IContentHasher
{
    Task<string> HashAsync(Stream stream, CancellationToken token);
}
public sealed class Sha256Hasher : IContentHasher
{
    public async Task<string> HashAsync(Stream stream, CancellationToken token) => Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
}
public sealed class Scanner(ScanStore store, IContentHasher hasher)
{
    private long discovered, processed, bytes, errors, groups;
    private readonly Stopwatch timer = new();
    private long lastReport;
    public async Task<string> ScanAsync(ScanProfile profile, IProgress<ScanProgress>? progress, CancellationToken token)
    {
        profile = ProfileValidation.Validate(profile);
        string scan = Guid.NewGuid().ToString("N");
        discovered = processed = bytes = errors = groups = lastReport = 0; timer.Restart(); store.Start(scan, profile);
        var timings = new Dictionary<string, long>();
        string phase = "Enumerating"; long phaseStarted = Stopwatch.GetTimestamp(), writesStarted = store.WriteTicks;
        var metadata = new List<FileRecord>(256);
        void Phase(string next)
        {
            timings[phase] = timings.GetValueOrDefault(phase) + Stopwatch.GetTimestamp() - phaseStarted;
            phase = next; phaseStarted = Stopwatch.GetTimestamp();
        }
        void Report(string phase, string activity, bool force = false)
        {
            if (!force && timer.ElapsedMilliseconds - lastReport < 150) return;
            lastReport = timer.ElapsedMilliseconds; progress?.Report(new(phase, discovered, processed, bytes, groups, errors, activity));
        }
        void Issue(string path, string reason) { errors++; store.Issue(scan, path, reason); }
        try
        {
            foreach (string root in PathPolicy.Roots(profile.Roots))
            {
                if (root.StartsWith(@"\\") && !profile.AllowNetwork) { Issue(root, "Network scanning disabled."); continue; }
                // Depth-first iterators keep directory listings incremental; no drive-sized queue.
                var stack = new Stack<IEnumerator<string>>();
                try
                {
                    EnsureNoReparseAncestors(root);
                    stack.Push(Directory.EnumerateFileSystemEntries(root).GetEnumerator());
                    while (stack.Count > 0)
                    {
                        token.ThrowIfCancellationRequested();
                        string path;
                        try { if (!stack.Peek().MoveNext()) { stack.Pop().Dispose(); continue; } path = stack.Peek().Current; }
                        catch (Exception ex) when (Expected(ex)) { Issue(root, ex.Message); stack.Pop().Dispose(); continue; }
                        try
                        {
                            var attr = File.GetAttributes(path);
                            if (profile.ExcludedDirectories.Any(p => PathPolicy.Within(path, p))) continue;
                            if ((attr & FileAttributes.ReparsePoint) != 0) { Issue(path, "Reparse point skipped; link traversal is disabled."); continue; }
                            if (!profile.AllowCloudHydration && ((int)attr & (0x1000 | 0x40000 | 0x400000)) != 0) { Issue(path, "Offline/cloud content skipped to avoid hydration."); continue; }
                            if ((!profile.IncludeHidden && attr.HasFlag(FileAttributes.Hidden)) || (!profile.IncludeSystem && attr.HasFlag(FileAttributes.System))) continue;
                            if (attr.HasFlag(FileAttributes.Directory)) { stack.Push(Directory.EnumerateFileSystemEntries(path).GetEnumerator()); continue; }
                            discovered++; Report("Enumerating", path);
                            string extension = System.IO.Path.GetExtension(path);
                            if (profile.IncludedExtensions.Length > 0 && !profile.IncludedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) || profile.ExcludedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) continue;
                            var info = new FileInfo(path);
                            if (info.Length == 0 ? !profile.IncludeEmpty : info.Length < profile.MinimumSize || info.Length > profile.MaximumSize) continue;
                            using var file = NativeFile.Open(path);
                            if (file.Record.Attributes.HasFlag(FileAttributes.ReparsePoint)) { Issue(path, "Replaced with reparse point."); continue; }
                            metadata.Add(file.Record);
                            if (metadata.Count == 256) { store.AddBatch(scan, metadata); metadata.Clear(); }
                            if (file.Record.Links > 1) Issue(path, "Hard link: physical identity deduplicated; automatic cleanup disabled.");
                        }
                        catch (Exception ex) when (Expected(ex)) { Issue(path, ex.Message); }
                    }
                }
                catch (Exception ex) when (Expected(ex)) { Issue(root, ex.Message); }
                finally { foreach (var iterator in stack) iterator.Dispose(); }
            }
            if (metadata.Count > 0) { store.AddBatch(scan, metadata); metadata.Clear(); }
            Report("Enumerated", $"{timer.Elapsed}", true);
            foreach (long size in store.CandidateSizes(scan))
            {
                Phase("Sampling");
                if (profile.Sample && size >= 1024 * 1024)
                {
                    foreach (var item in store.Files(scan, size))
                    {
                        token.ThrowIfCancellationRequested();
                        try
                        {
                            using var file = OpenSnapshot(item.File);
                            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                            byte[] buffer = new byte[4096];
                            foreach (long position in new[] { 0L, (size - buffer.Length) / 2, size - buffer.Length })
                            { file.Stream.Position = position; await file.Stream.ReadExactlyAsync(buffer, token); hash.AppendData(buffer); bytes += buffer.Length; }
                            file.CheckStable();
                            store.Execute("UPDATE files SET sample=$sample WHERE id=$id", ("$sample", Convert.ToHexString(hash.GetHashAndReset())), ("$id", item.Id));
                        }
                        catch (Exception ex) when (Expected(ex)) { Issue(item.File.Path, ex.Message); }
                        Report("Sampling", item.File.Path);
                    }
                }
                Phase("Hashing");
                foreach (var item in store.Files(scan, size))
                {
                    token.ThrowIfCancellationRequested();
                    if (profile.Sample && size >= 1024 * 1024 && !SampleMatches(item.Id, scan)) continue;
                    try
                    {
                        using var file = OpenSnapshot(item.File);
                        using var measured = new ReadMeter(file.Stream, count => bytes += count);
                        string hash = await hasher.HashAsync(measured, token); file.CheckStable();
                        store.Execute("UPDATE files SET hash=$hash WHERE id=$id", ("$hash", hash), ("$id", item.Id));
                    }
                    catch (Exception ex) when (Expected(ex)) { Issue(item.File.Path, ex.Message); }
                    processed++; Report("Hashing", item.File.Path);
                }
                Phase("Byte verification");
                foreach (var item in store.Files(scan, size))
                {
                    token.ThrowIfCancellationRequested(); if (item.Hash is null) continue;
                    long group = item.Id;
                    try
                    {
                        using var current = OpenSnapshot(item.File);
                        foreach (var representative in Representatives(scan, size, item.Hash))
                        {
                            try
                            {
                                using var other = OpenSnapshot(representative.File);
                                if (await NativeFile.EqualAsync(current, other, token, count => bytes += count)) { group = representative.Id; break; }
                            }
                            catch (Exception ex) when (Expected(ex))
                            {
                                Issue(representative.File.Path, ex.Message);
                                // A failed representative invalidates its entire provisional group.
                                store.Execute("UPDATE files SET group_id=NULL WHERE scan=$scan AND group_id=$id", ("$scan", scan), ("$id", representative.Id));
                            }
                        }
                        current.CheckStable();
                        store.Execute("UPDATE files SET group_id=$group WHERE id=$id", ("$group", group), ("$id", item.Id));
                    }
                    catch (Exception ex) when (Expected(ex)) { Issue(item.File.Path, ex.Message); }
                    Report("Byte verification", item.File.Path);
                }
            }
            Phase("Finalizing");
            store.Execute("UPDATE files SET group_id=NULL WHERE scan=$scan AND group_id IN (SELECT group_id FROM files WHERE scan=$scan GROUP BY group_id HAVING count(*)<2)", ("$scan", scan));
            using (var db = store.Open()) { using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT count(DISTINCT group_id) FROM files WHERE scan=$scan"; cmd.Parameters.AddWithValue("$scan", scan); groups = (long)cmd.ExecuteScalar()!; }
            store.Execute("UPDATE scans SET state='Complete' WHERE id=$scan", ("$scan", scan)); Report("Complete", $"Elapsed {timer.Elapsed}; review results before cleanup.", true); return scan;
        }
        catch (OperationCanceledException) { store.Execute("UPDATE scans SET state='Cancelled; rescan required' WHERE id=$scan", ("$scan", scan)); Report("Cancelled", "Partial results are historical only.", true); throw; }
        catch { store.Execute("UPDATE scans SET state='Failed; rescan required' WHERE id=$scan", ("$scan", scan)); throw; }
        finally
        {
            Phase("Stopped");
            timings["Database writes (overlaps phases)"] = store.WriteTicks - writesStarted;
            foreach (var measurement in timings)
                store.Execute("INSERT OR REPLACE INTO scan_metrics(scan,phase,elapsed_ms) VALUES($scan,$phase,$elapsed)", ("$scan", scan), ("$phase", measurement.Key), ("$elapsed", (long)(measurement.Value * 1000d / Stopwatch.Frequency)));
        }
    }
    private bool SampleMatches(long id, string scan)
    {
        using var db = store.Open(); using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT count(*) FROM files WHERE scan=$scan AND length=(SELECT length FROM files WHERE id=$id) AND sample=(SELECT sample FROM files WHERE id=$id)"; cmd.Parameters.AddWithValue("$scan", scan); cmd.Parameters.AddWithValue("$id", id); return (long)cmd.ExecuteScalar()! > 1;
    }
    private IEnumerable<(long Id, FileRecord File)> Representatives(string scan, long size, string hash)
    {
        using var db = store.Open(); using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT group_id,record FROM files WHERE scan=$scan AND length=$size AND hash=$hash AND id=group_id ORDER BY id"; cmd.Parameters.AddWithValue("$scan", scan); cmd.Parameters.AddWithValue("$size", size); cmd.Parameters.AddWithValue("$hash", hash);
        using var reader = cmd.ExecuteReader(); while (reader.Read()) yield return (reader.GetInt64(0), System.Text.Json.JsonSerializer.Deserialize<FileRecord>(reader.GetString(1))!);
    }
    public static NativeFile OpenSnapshot(FileRecord record, bool action = false)
    {
        EnsureNoReparseAncestors(record.Path);
        var file = NativeFile.Open(record.Path, action);
        if (!NativeFile.SameSnapshot(record, file.Record)) { file.Dispose(); throw new IOException("Stale file identity or metadata; rescan required."); }
        return file;
    }
    public static void EnsureNoReparseAncestors(string path)
    {
        for (string? current = System.IO.Path.GetFullPath(path); current != null; current = System.IO.Path.GetDirectoryName(current))
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse path is not supported: " + current);
    }
    internal static bool Expected(Exception ex) => ex is IOException or UnauthorizedAccessException or Win32Exception or System.Security.SecurityException;
}
