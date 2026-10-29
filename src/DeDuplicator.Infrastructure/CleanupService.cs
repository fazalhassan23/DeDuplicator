using System.Text.Json;
using DeDuplicator.Core;
using System.Security.Cryptography;

namespace DeDuplicator.Infrastructure;

public sealed record JournalEntry(string Id, string Source, string? Destination, string Action, string State, string Detail, FileRecord Record, string Hash, RecycleReceipt? Recycle = null);
public class CleanupService(ScanStore store, IFileRecycler? recycler = null)
{
    private readonly IFileRecycler recycler = recycler ?? new WindowsRecycler();
    public async Task<IReadOnlyList<string>> ExecuteAsync(ActionPlan plan, bool permanentEnabled, IProgress<string>? progress, CancellationToken token)
    {
        var sources = plan.Files.Select(f => f.Source.Identity).ToHashSet(StringComparer.Ordinal);
        if (sources.Count != plan.Files.Count || plan.Files.Any(f => sources.Contains(f.Keeper.Identity))) throw new IOException("Invalid plan: a retained keeper is also scheduled for removal or a source occurs twice.");
        var results = new List<string>();
        foreach (var operation in plan.Files)
        {
            token.ThrowIfCancellationRequested();
            string id = Guid.NewGuid().ToString("N");
            bool stagedForRecycle = false;
            string completionDetail = "";
            store.Execute("INSERT INTO journal(id,plan,source,destination,action,state,detail,record,created,hash,keeper_record) VALUES($id,$plan,$source,$destination,$action,'Started','',$record,$created,'',$keeper)", ("$id", id), ("$plan", plan.Id.ToString()), ("$source", operation.Source.Path), ("$destination", operation.Destination), ("$action", operation.Action.ToString()), ("$record", JsonSerializer.Serialize(operation.Source)), ("$created", DateTime.UtcNow.ToString("O")), ("$keeper", JsonSerializer.Serialize(operation.Keeper)));
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
                        string destination = operation.Destination ?? throw new IOException("Recycle requires a reviewed staging destination.");
                        await QuarantineAsync(source, destination, id, token);
                        stagedForRecycle = true; source.Dispose();
                        SetState(id, "Staged for recycling", "Source safely quarantined before shell recycling; inspect both paths after interruption.");
                        FileRecord staged;
                        using (var probe = NativeFile.Open(destination)) staged = probe.Record;
                        if (!NativeFile.SameSnapshot(operation.Source, staged)) throw new IOException("Recycle staging identity changed; inspect the journal and staged path.");
                        var receipt = await recycler.RecycleAsync(staged, keeper, token);
                        store.Execute("UPDATE journal SET recycle_receipt=$receipt WHERE id=$id", ("$receipt", JsonSerializer.Serialize(receipt)), ("$id", id));
                        completionDetail = "Recycled; restore to the original path using this journal entry. Recycle item: " + receipt.FilePath;
                        break;
                    default: throw new IOException("Unsupported action.");
                }
                SetState(id, "Complete", completionDetail); results.Add("Complete: " + operation.Source.Path);
            }
            catch (OperationCanceledException) { SetState(id, "Interrupted", "Cancelled; inspect destination and source before recovery."); throw; }
            catch (Exception ex) when (Scanner.Expected(ex))
            {
                string state = !stagedForRecycle ? "Failed" : File.Exists(operation.Destination) ? "Recycle retained" : "Recycle outcome requires inspection";
                SetState(id, state, ex.Message); results.Add((!stagedForRecycle ? "Skipped" : state) + ": " + operation.Source.Path + " — " + ex.Message);
            }
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
            // Keep the verified destination write/delete locked until the source is removed.
            RemoveCopiedSource(source, destination);
        }
    }
    protected virtual void Rename(NativeFile source, string destination) => source.RenameByHandle(destination);
    protected virtual void RemoveCopiedSource(NativeFile source, string destination) { source.CheckStable(); source.DeleteByHandle(); }
    public IReadOnlyList<JournalEntry> Journal()
    {
        using var db = store.Open(); using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT id,source,destination,action,state,detail,record,hash,recycle_receipt FROM journal ORDER BY created DESC LIMIT 1000";
        using var reader = cmd.ExecuteReader(); var list = new List<JournalEntry>();
        while (reader.Read()) list.Add(new(reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), JsonSerializer.Deserialize<FileRecord>(reader.GetString(6))!, reader.GetString(7), reader.IsDBNull(8) ? null : JsonSerializer.Deserialize<RecycleReceipt>(reader.GetString(8)))); return list;
    }
    public async Task RestoreAsync(JournalEntry entry, CancellationToken token)
    {
        if (entry.Action == nameof(CleanupAction.Recycle) && entry.Recycle is not null && entry.State != "Restored")
        {
            SetState(entry.Id, "Restoring", "Restoring recycled content without overwrite.");
            try { await recycler.RestoreAsync(entry.Recycle, entry.Source, entry.Hash, entry.Record.Length, token); SetState(entry.Id, "Restored", "Original path restored from Recycle Bin."); }
            catch (Exception ex) { SetState(entry.Id, "Restore failed", ex.Message); throw; }
            return;
        }
        bool eligible = entry.Action == nameof(CleanupAction.Quarantine) && entry.State is not ("Restored" or "Restoring") || entry.Action == nameof(CleanupAction.Recycle) && entry.State != "Complete" && entry.State != "Restored";
        if (!eligible || entry.Destination is null) throw new IOException("This journal entry has no recoverable quarantine destination. Inspect Windows Recycle Bin or both recorded paths.");
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
