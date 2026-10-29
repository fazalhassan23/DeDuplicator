using System.Windows;
using DeDuplicator.Core;
using DeDuplicator.Infrastructure;
using Microsoft.Win32;

namespace DeDuplicator.App;

public interface IUserDialogs
{
    string[] ChooseFolders();
    string? OpenProfile();
    string? SaveProfile();
    (string Path, bool Json)? SaveExport();
    bool ConfirmCleanup(ActionPlan plan);
    bool ConfirmRestore(JournalEntry entry);
    bool ConfirmOpen(string path);
}

public sealed class WindowsUserDialogs : IUserDialogs
{
    public string[] ChooseFolders()
    {
        var dialog = new OpenFolderDialog { Title = "Choose a folder or drive", Multiselect = true };
        return dialog.ShowDialog() == true ? dialog.FolderNames : [];
    }
    public string? OpenProfile() { var dialog = new OpenFileDialog { Filter = "Scan profile|*.json" }; return dialog.ShowDialog() == true ? dialog.FileName : null; }
    public string? SaveProfile() { var dialog = new SaveFileDialog { Filter = "Scan profile|*.json" }; return dialog.ShowDialog() == true ? dialog.FileName : null; }
    public (string Path, bool Json)? SaveExport()
    {
        var dialog = new SaveFileDialog { Filter = "CSV|*.csv|JSON|*.json", FileName = "duplicates", OverwritePrompt = false };
        return dialog.ShowDialog() == true ? (dialog.FileName, dialog.FilterIndex == 2) : null;
    }
    public bool ConfirmCleanup(ActionPlan plan)
    {
        string recovery = plan.Files.Any(f => f.Action == CleanupAction.PermanentDelete) ? "PERMANENT DELETION CANNOT BE UNDONE." : "Inspect per-file results in the recovery journal. Recycling first stages each file; unavailable recycling retains it there.";
        return MessageBox.Show($"Execute the displayed plan for {plan.Files.Count} files?\n\n{recovery}", "Confirm reviewed plan", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
    }
    public bool ConfirmRestore(JournalEntry entry) => MessageBox.Show("Restore to the original path? Existing files are never overwritten.\n\n" + entry.Source, "Restore file", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
    public bool ConfirmOpen(string path) => MessageBox.Show("Open this file in its associated application? Only open files you trust.\n\n" + path, "Open file", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
}
