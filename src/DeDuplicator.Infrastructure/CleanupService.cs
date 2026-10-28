using System.Text.Json;
using DeDuplicator.Core;
using System.Security.Cryptography;

namespace DeDuplicator.Infrastructure;

public sealed record JournalEntry(string Id, string Source, string? Destination, string Action, string State, string Detail, FileRecord Record, string Hash);
public class CleanupService(ScanStore store)
{
    public async Task<IReadOnlyList<string>> ExecuteAsync(ActionPlan plan, bool permanentEnabled, IProgress<string>? progress, CancellationToken token)
    {
        var sources = plan.Files.Select(f => f.Source.Identity).ToHashSet(StringComparer.Ordinal);
        if (sources.Count != plan.Files.Count || plan.Files.Any(f => sources.Contains(f.Keeper.Identity))) throw new IOException("Invalid plan: a retained keeper is also scheduled for removal or a source occurs twice.");
        var results = new List<string>();
        foreach (var operation in plan.Files)
        {
            token.ThrowIfCancellationRequested();
            string id = Guid.NewGuid().ToString("N");
            store.Execute("INSERT INTO journal(id,plan,source,destination,action,state,detail,record,created,hash) VALUES($id,$plan,$source,$destination,$action,'Started','',$record,$created,'')", ("$id", id), ("$plan", plan.Id.ToString()), ("$source", operation.Source.Path), ("$destination", operation.Destination), ("$action", operation.Action.ToString()), ("$record", JsonSerializer.Serialize(operation.Source)), ("$created", DateTime.UtcNow.ToString("O")));
            try
            {
                if (PathPolicy.Protected(operation.Source.Path, plan.Profile) || operation.Source.ReviewRequired) throw new IOException("Protected or review-required file.");
                if (operation.Source.Identity == operation.Keeper.Identity) throw new IOException("Same physical file.");
                // The keeper remains open without write/delete sharing through the operation.
                using var keeper = Scanner.OpenSnapshot(operation.Keeper);
                using var source = Scanner.OpenSnapshot(operation.Source, true);
                if (source.Record.ReviewRequired || keeper.Record.ReviewRequired || source.Security != keeper.Security || source.Record.Attributes != keeper.Record.Attributes) throw new IOException("Stream, link, permission or attribute differences require manual review.");
                if (!await NativeFile.EqualAsync(source, keeper, token)) throw new IOException("Contents no longer match.");
                source.Stream.Position = 0;
                string contentHash = Convert.ToHexString(await SHA256.HashDataAsync(source.Stream, token));
                source.CheckStable();
                store.Execute("UPDATE journal SET hash=$hash WHERE id=$id", ("$hash", contentHash), ("$id", id));
                token.ThrowIfCancellationRequested();
                switch (operation.Action)
                {
                    case CleanupAction.Quarantine:
                        await QuarantineAsync(source, operation.Destination ?? throw new IOException("Missing destination."), id, token);
                        break;
                    case CleanupAction.PermanentDelete:
                        if (!permanentEnabled) throw new IOException("Advanced permanent deletion is disabled.");
                        source.DeleteByHandle();
                        break;
                    case CleanupAction.Recycle:
                        // The shell takes a path, so it cannot operate while our delete-denying source handle is open.
                        // Refuse rather than expose a replacement-file race between validation and shell deletion.
                        throw new IOException("Recycle Bin execution is unavailable under the handle-based safety policy. Choose quarantine explicitly.");
                    default: throw new IOException("Unsupported action.");
                }
                SetState(id, "Complete", ""); results.Add("Complete: " + operation.Source.Path);
            }
            catch (OperationCanceledException) { SetState(id, "Interrupted", "Cancelled; inspect destination and source before recovery."); throw; }
            catch (Exception ex) when (Scanner.Expected(ex)) { SetState(id, "Failed", ex.Message); results.Add("Skipped: " + operation.Source.Path + " — " + ex.Message); }
            progress?.Report(results[^1]);
        }
        return results;
    }
    private async Task QuarantineAsync(NativeFile source, string destination, string journalId, CancellationToken token)
    {
        if (File.Exists(destination) || Directory.Exists(destination)) throw new IOException("Destination already exists.");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
        Scanner.EnsureNoReparseAncestors(System.IO.Path.GetDirectoryName(destination)!);
        try { Rename(source, destination); return; }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 17) { /* Cross-device rename: copy, verify, then delete through validated source handle. */ }
        string partial = destination + ".partial";
        await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            source.Stream.Position = 0; await source.Stream.CopyToAsync(output, 131072, token); await output.FlushAsync(token); output.Flush(true);
        }
        SetState(journalId, "Copied", "Partial copy at " + partial + "; source retained until verification and promotion.");
        using (var copy = NativeFile.Open(partial, true))
        {
            if (!await NativeFile.EqualAsync(source, copy, token)) throw new IOException("Quarantine copy verification failed; source retained.");
            Rename(copy, destination);
        }
        source.CheckStable(); source.DeleteByHandle();
    }
    protected virtual void Rename(NativeFile source, string destination) => source.RenameByHandle(destination);
    public IReadOnlyList<JournalEntry> Journal()
    {
        using var db = store.Open(); using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT id,source,destination,action,state,detail,record,hash FROM journal ORDER BY created DESC LIMIT 1000";
        using var reader = cmd.ExecuteReader(); var list = new List<JournalEntry>();
        while (reader.Read()) list.Add(new(reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), JsonSerializer.Deserialize<FileRecord>(reader.GetString(6))!, reader.GetString(7))); return list;
    }
    public async Task RestoreAsync(JournalEntry entry, CancellationToken token)
    {
        if (entry.Action != nameof(CleanupAction.Quarantine) || entry.State != "Complete" || entry.Destination is null) throw new IOException("Only completed quarantine entries can be restored automatically. Inspect interrupted operations manually.");
        if (File.Exists(entry.Source) || Directory.Exists(entry.Source)) throw new IOException("Restore path occupied. Resolve the conflict explicitly; no file was overwritten.");
        Scanner.EnsureNoReparseAncestors(System.IO.Path.GetDirectoryName(entry.Source)!);
        using var source = NativeFile.Open(entry.Destination, true);
        if (source.Record.ReviewRequired) throw new IOException("Quarantine file has links, streams or attributes requiring manual recovery.");
        if (source.Record.Length != entry.Record.Length) throw new IOException("Quarantined content changed; manual recovery required.");
        if (entry.Hash.Length == 0 || Convert.ToHexString(await SHA256.HashDataAsync(source.Stream, token)) != entry.Hash) throw new IOException("Quarantined content changed; manual recovery required.");
        source.CheckStable();
        SetState(entry.Id, "Restoring", "Restore requested; destination will never be overwritten.");
        try { await QuarantineAsync(source, entry.Source, entry.Id, token); SetState(entry.Id, "Restored", "Original path restored."); }
        catch (Exception ex) { SetState(entry.Id, "Restore failed", ex.Message); throw; }
    }
    private void SetState(string id, string state, string detail) => store.Execute("UPDATE journal SET state=$state,detail=$detail WHERE id=$id", ("$state", state), ("$detail", detail), ("$id", id));
}
