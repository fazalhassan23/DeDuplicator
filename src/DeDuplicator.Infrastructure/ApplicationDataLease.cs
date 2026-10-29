namespace DeDuplicator.Infrastructure;

/// <summary>Prevents concurrent startup recovery against one data directory across Windows sessions.</summary>
public sealed class ApplicationDataLease : IDisposable
{
    private readonly FileStream lease;

    public ApplicationDataLease(string directory)
    {
        Directory.CreateDirectory(directory);
        // Keep the file rather than unlinking it: deleting a lock path creates a new-owner race.
        lease = new FileStream(Path.Combine(directory, ".instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    public void Dispose() => lease.Dispose();
}
