namespace DeDuplicator.Core;

public sealed record ScanProfile
{
    public string[] Roots { get; init; } = [];
    public string[] ExcludedDirectories { get; init; } = [];
    public string[] ProtectedDirectories { get; init; } = [];
    public string[] IncludedExtensions { get; init; } = [];
    public string[] ExcludedExtensions { get; init; } = [];
    public long MinimumSize { get; init; } = 1;
    public long MaximumSize { get; init; } = long.MaxValue;
    public bool IncludeHidden { get; init; }
    public bool IncludeSystem { get; init; }
    public bool IncludeEmpty { get; init; }
    public bool AllowNetwork { get; init; }
    public bool AllowCloudHydration { get; init; }
    public bool Sample { get; init; } = true;
}
public sealed record FileRecord(string Path, long Length, DateTime ModifiedUtc, string Identity, uint Links, FileAttributes Attributes, bool ReviewRequired);
public sealed record DuplicateGroup(long Id, IReadOnlyList<FileRecord> Files, string Hash, bool Partial = false);
public sealed record ScanProgress(string Phase, long Discovered, long Processed, long BytesRead, long Groups, long Errors, string Activity);
public enum KeepRule { ShortestPath, Oldest, Newest, PreferredDirectory }
public enum CleanupAction { Quarantine, Recycle, PermanentDelete }
public sealed record PlannedFile(FileRecord Source, FileRecord Keeper, CleanupAction Action, string? Destination, string Reason);
public sealed record ActionPlan(Guid Id, DateTime CreatedUtc, IReadOnlyList<PlannedFile> Files, ScanProfile Profile);

public static class PathPolicy
{
    public static string Normalize(string path) => System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path));
    public static bool Within(string path, string directory) => Normalize(path).Equals(Normalize(directory), StringComparison.OrdinalIgnoreCase) || Normalize(path).StartsWith(Normalize(directory) + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    public static string[] Roots(IEnumerable<string> roots) => roots.Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p.Length).Aggregate(new List<string>(), (list, path) => { if (!list.Any(parent => Within(path, parent))) list.Add(path); return list; }).ToArray();
    public static bool Protected(string path, ScanProfile profile) => profile.ProtectedDirectories.Concat(new[] { Environment.GetFolderPath(Environment.SpecialFolder.Windows), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages"), System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"), System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeDuplicator") }).Where(p => !string.IsNullOrWhiteSpace(p)).Any(p => Within(path, p));
}

public static class Planner
{
    public static ActionPlan Create(IEnumerable<DuplicateGroup> groups, ScanProfile profile, KeepRule rule, CleanupAction action, string quarantine, string preferred = "")
    {
        var planned = new List<PlannedFile>();
        var id = Guid.NewGuid();
        foreach (var group in groups)
        {
            var files = group.Files;
            if (files.Count < 2) continue;
            var keeper = files.OrderByDescending(f => PathPolicy.Protected(f.Path, profile) || f.ReviewRequired)
                .ThenBy(f => rule switch { KeepRule.Oldest => f.ModifiedUtc.Ticks, KeepRule.Newest => -f.ModifiedUtc.Ticks, KeepRule.PreferredDirectory => !string.IsNullOrWhiteSpace(preferred) && PathPolicy.Within(f.Path, preferred) ? 0L : 1L, _ => f.Path.Length })
                .ThenBy(f => f.Path, StringComparer.OrdinalIgnoreCase).First();
            foreach (var file in files.Where(f => f != keeper && !f.ReviewRequired && !PathPolicy.Protected(f.Path, profile)))
                planned.Add(new(file, keeper, action, action == CleanupAction.Quarantine ? System.IO.Path.Combine(PathPolicy.Normalize(quarantine), id.ToString("N"), Guid.NewGuid().ToString("N") + ".file") : null, $"Keep {keeper.Path}: {rule}; protected/review files retained."));
        }
        return new(id, DateTime.UtcNow, planned, profile);
    }
}
