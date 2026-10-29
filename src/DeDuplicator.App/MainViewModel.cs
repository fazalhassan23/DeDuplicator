using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using DeDuplicator.Core;
using DeDuplicator.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace DeDuplicator.App;
public sealed class ResultRow(long groupId, FileRecord file, ScanProfile profile, bool partial = false) : Observable
{
    public long GroupId { get; } = groupId;
    public FileRecord File { get; } = file;
    public bool Partial { get; } = partial;
    public string Safety => PathPolicy.Protected(File.Path, profile) ? "Protected" : File.ReviewRequired ? "Manual review" : Partial ? "Verified (page)" : "Verified at scan";
    private bool selected;
    public bool Selected { get => selected; set => Set(ref selected, value); }
}
public sealed record HistoryRow(string Id, string Label);
public sealed class MainViewModel : Observable
{
    private readonly ScanStore store;
    private readonly Scanner scanner;
    private readonly CleanupService cleanup;
    private readonly ILogger<MainViewModel> logger;
    private readonly IUserDialogs dialogs;
    public Task Initialization { get; }
    private CancellationTokenSource? cancellation;
    private ScanProfile? scannedProfile;
    private string? scanId;
    private bool fresh;
    private int page;
    private int memberOffset;
    private long? memberGroup;
    private ActionPlan? plan;
    private string roots = "", status = "Choose locations to begin", progressText = "Exact matching • SHA-256 candidates • complete byte verification", details = "", pageText = "No scan results yet.", planText = "No action plan prepared.";
    private bool busy;
    private int tab;
    public string Roots { get => roots; set => Set(ref roots, value); }
    public string Status { get => status; set => Set(ref status, value); }
    public string ProgressText { get => progressText; set => Set(ref progressText, value); }
    public string Details { get => details; set => Set(ref details, value); }
    public string PageText { get => pageText; set => Set(ref pageText, value); }
    public string PlanText { get => planText; set => Set(ref planText, value); }
    public bool Busy { get => busy; private set { Set(ref busy, value); CommandManager.InvalidateRequerySuggested(); } }
    public int Tab { get => tab; set => Set(ref tab, value); }
    public bool IncludeEmpty { get; set; }
    public bool IncludeHidden { get; set; }
    public bool IncludeSystem { get; set; }
    public bool AllowNetwork { get; set; }
    public bool AllowCloud { get; set; }
    public bool Sample { get; set; } = true;
    public string Minimum { get; set; } = "1";
    public string Maximum { get; set; } = "";
    public string IncludedExtensions { get; set; } = "";
    public string ExcludedExtensions { get; set; } = "";
    public string Exclusions { get; set; } = "";
    public string Protections { get; set; } = "";
    public string Search { get; set; } = "";
    public string Sort { get; set; } = "space";
    public string[] Sorts { get; } = ["space", "count", "size", "location"];
    public KeepRule[] Rules { get; } = Enum.GetValues<KeepRule>();
    public KeepRule Rule { get; set; } = KeepRule.ShortestPath;
    public string Preferred { get; set; } = "";
    public string Quarantine { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeDuplicator", "Quarantine");
    public CleanupAction[] Actions { get; } = Enum.GetValues<CleanupAction>();
    public CleanupAction Action { get; set; }
    public bool PermanentEnabled { get; set; }
    public ResultRow? SelectedRow { get; set; }
    public HistoryRow? SelectedHistory { get; set; }
    public JournalEntry? SelectedJournal { get; set; }
    public ObservableCollection<ResultRow> Rows { get; } = [];
    public ObservableCollection<PlannedFile> PlanRows { get; } = [];
    public ObservableCollection<HistoryRow> History { get; } = [];
    public ObservableCollection<JournalEntry> Journal { get; } = [];
    public ICommand AddFolder { get; }
    public ICommand Scan { get; }
    public ICommand Cancel { get; }
    public ICommand Refresh { get; }
    public ICommand Next { get; }
    public ICommand Previous { get; }
    public ICommand NextMembers { get; }
    public ICommand PreviousMembers { get; }
    public ICommand SelectRule { get; }
    public ICommand KeepSelected { get; }
    public ICommand Review { get; }
    public ICommand Execute { get; }
    public ICommand Restore { get; }
    public ICommand Export { get; }
    public ICommand LoadProfile { get; }
    public ICommand SaveProfile { get; }
    public ICommand RefreshHistory { get; }
    public ICommand ViewHistory { get; }
    public ICommand ResumeHistory { get; }
    public ICommand OpenFile { get; }
    public ICommand OpenFolder { get; }
    public ICommand CopyPath { get; }

    public MainViewModel(ScanStore store, Scanner scanner, CleanupService cleanup, ILogger<MainViewModel> logger, IUserDialogs? dialogs = null)
    {
        this.store = store; this.scanner = scanner; this.cleanup = cleanup; this.logger = logger;
        this.dialogs = dialogs ?? new WindowsUserDialogs();
        bool Idle() => !Busy;
        Command Sync(Action action) => new(_ => { try { action(); } catch (Exception ex) { Failed(ex); } }, Idle);
        AsyncCommand Async(Func<Task> action) => new(async () => { Busy = true; try { await action(); } catch (OperationCanceledException) { Status = "Cancelled — inspect the journal for any partial file operations."; throw; } finally { Busy = false; } }, Failed, Idle);
        AddFolder = Sync(() => Roots = string.Join(Environment.NewLine, Lines(Roots).Concat(this.dialogs.ChooseFolders())));
        Scan = Async(ScanAsync);
        Cancel = new Command(_ => cancellation?.Cancel(), () => Busy);
        Refresh = Async(async () => { page = 0; await LoadResultsAsync(); });
        Next = Async(async () => { page++; await LoadResultsAsync(); });
        Previous = Async(async () => { page = Math.Max(0, page - 1); await LoadResultsAsync(); });
        NextMembers = Async(async () => await LoadMembersAsync(500));
        PreviousMembers = Async(async () => await LoadMembersAsync(-500));
        SelectRule = Sync(Propose);
        KeepSelected = Sync(() => { if (SelectedRow is not { } row) return; foreach (var member in Rows.Where(r => r.GroupId == row.GroupId)) member.Selected = member != row && member.Safety.StartsWith("Verified", StringComparison.Ordinal); InvalidatePlan(); });
        Review = Sync(BuildPlan);
        Execute = Async(ExecuteAsync);
        Restore = Async(RestoreAsync);
        Export = Async(ExportAsync);
        RefreshHistory = Async(async () => await LoadHistoryAsync());
        ViewHistory = Async(async () => { if (SelectedHistory is null) return; scanId = SelectedHistory.Id; scannedProfile = ReadProfile(scanId); fresh = false; page = 0; InvalidatePlan(); await LoadResultsAsync(); Tab = 1; Status = "Historical results — rescan required before planning"; });
        ResumeHistory = Sync(() => { if (SelectedHistory is null) return; ApplyProfile(ReadProfile(SelectedHistory.Id)); Tab = 0; Status = "Historical settings loaded. Scan again to revalidate all filesystem assumptions."; });
        SaveProfile = Async(async () => { var profile = Profile(); string? path = this.dialogs.SaveProfile(); if (path != null) await File.WriteAllTextAsync(path, JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true })); });
        LoadProfile = Async(async () => { string? path = this.dialogs.OpenProfile(); if (path != null) { if (new FileInfo(path).Length > 1048576) throw new IOException("Profile files must be at most 1 MiB."); ApplyProfile(JsonSerializer.Deserialize<ScanProfile>(await File.ReadAllTextAsync(path)) ?? throw new IOException("Invalid profile.")); } });
        CopyPath = Sync(() => { if (SelectedRow != null) Clipboard.SetText(SelectedRow.File.Path); });
        OpenFolder = Sync(() => { if (SelectedRow != null) Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = "/select,\"" + SelectedRow.File.Path + "\"", UseShellExecute = true }); });
        OpenFile = Sync(() =>
        {
            if (SelectedRow is not { } row) return;
            if (this.dialogs.ConfirmOpen(row.File.Path)) Process.Start(new ProcessStartInfo(row.File.Path) { UseShellExecute = true });
        });
        Initialization = InitializeAsync();
    }
    private async Task InitializeAsync() { try { await LoadHistoryAsync(); } catch (Exception ex) { Failed(ex); } }
    private void Failed(Exception ex) { logger.LogError("Application operation failed ({Type}): {Message}", ex.GetType().Name, ex.Message); Status = "Operation could not complete"; Details = ex.Message; }
    private static string[] Lines(string text) => text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static string[] Extensions(string text) => text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.StartsWith('.') ? x : "." + x).ToArray();
    private ScanProfile Profile()
    {
        if (!long.TryParse(Minimum, out long min) || !string.IsNullOrWhiteSpace(Maximum) && !long.TryParse(Maximum, out _)) throw new IOException("Enter whole numbers for minimum and maximum file sizes.");
        long max = string.IsNullOrWhiteSpace(Maximum) ? long.MaxValue : long.Parse(Maximum);
        if (min < 0 || max < min) throw new IOException("Check the minimum and maximum sizes.");
        return ProfileValidation.Validate(new() { Roots = PathPolicy.Roots(Lines(Roots)), MinimumSize = min, MaximumSize = max, IncludeEmpty = IncludeEmpty, IncludeHidden = IncludeHidden, IncludeSystem = IncludeSystem, AllowNetwork = AllowNetwork, AllowCloudHydration = AllowCloud, Sample = Sample, IncludedExtensions = Extensions(IncludedExtensions), ExcludedExtensions = Extensions(ExcludedExtensions), ExcludedDirectories = Lines(Exclusions).Select(PathPolicy.Normalize).ToArray(), ProtectedDirectories = Lines(Protections).Select(PathPolicy.Normalize).ToArray() }, false);
    }
    private void ApplyProfile(ScanProfile p)
    {
        p = ProfileValidation.Validate(p, false);
        Roots = string.Join(Environment.NewLine, p.Roots); Minimum = p.MinimumSize.ToString(); Maximum = p.MaximumSize == long.MaxValue ? "" : p.MaximumSize.ToString(); IncludeEmpty = p.IncludeEmpty; IncludeHidden = p.IncludeHidden; IncludeSystem = p.IncludeSystem; AllowNetwork = p.AllowNetwork; AllowCloud = p.AllowCloudHydration; Sample = p.Sample; IncludedExtensions = string.Join(',', p.IncludedExtensions); ExcludedExtensions = string.Join(',', p.ExcludedExtensions); Exclusions = string.Join(Environment.NewLine, p.ExcludedDirectories); Protections = string.Join(Environment.NewLine, p.ProtectedDirectories);
        foreach (string property in new[] { nameof(Minimum), nameof(Maximum), nameof(IncludeEmpty), nameof(IncludeHidden), nameof(IncludeSystem), nameof(AllowNetwork), nameof(AllowCloud), nameof(Sample), nameof(IncludedExtensions), nameof(ExcludedExtensions), nameof(Exclusions), nameof(Protections) }) Notify(property);
    }
    private ScanProfile ReadProfile(string id)
    {
        using var db = store.Open(); using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT profile FROM scans WHERE id=$id"; cmd.Parameters.AddWithValue("$id", id); return JsonSerializer.Deserialize<ScanProfile>((string)cmd.ExecuteScalar()!)!;
    }
    private async Task ScanAsync()
    {
        await Initialization;
        var profile = Profile(); if (profile.Roots.Length == 0) throw new IOException("Choose at least one location.");
        fresh = false; InvalidatePlan(); Rows.Clear(); cancellation = new(); var watch = Stopwatch.StartNew();
        var progress = new Progress<ScanProgress>(p => { Status = p.Phase; ProgressText = $"{p.Discovered:N0} discovered · {p.Processed:N0} hashed · {p.BytesRead:N0} bytes read · {p.Groups:N0} groups · {p.Errors:N0} issues · {watch.Elapsed:mm\\:ss}"; Details = p.Activity; });
        try { scanId = await Task.Run(() => scanner.ScanAsync(profile, progress, cancellation.Token)); scannedProfile = profile; fresh = true; page = 0; await LoadResultsAsync(); Details = await Task.Run(() => store.Diagnostics(scanId)); Tab = 1; }
        catch (OperationCanceledException) { Status = "Scan cancelled; partial history retained. Rescan to continue."; }
        finally { cancellation.Dispose(); cancellation = null; await LoadHistoryAsync(); }
    }
    private async Task LoadResultsAsync()
    {
        if (scanId is null || scannedProfile is null) return;
        var result = await Task.Run(() => store.Groups(scanId, page * 25, 25, Search, Sort));
        memberGroup = null; memberOffset = 0;
        Rows.Clear(); foreach (var group in result) foreach (var file in group.Files) Rows.Add(new(group.Id, file, scannedProfile, group.Partial));
        PageText = $"Page {page + 1} · {result.Count} groups · {result.Sum(g => g.Files.Count):N0} displayed files · {result.Sum(g => g.Files[0].Length * (g.Files.Count - 1)):N0} logical redundant bytes in displayed subsets. Large groups show 500 copies at a time; planning retains one keeper in each displayed subset. " + (fresh ? "Verified at scan time." : "Historical; verification is stale."); InvalidatePlan();
    }
    private async Task LoadMembersAsync(int delta)
    {
        if (scanId is null || scannedProfile is null) return;
        long group = memberGroup ?? SelectedRow?.GroupId ?? throw new IOException("Select a large group first.");
        int offset = memberGroup is null ? (delta > 0 ? 500 : 0) : Math.Max(0, memberOffset + delta);
        var result = await Task.Run(() => store.GroupMembers(scanId, group, offset));
        if (result.Files.Count == 0) { Status = "No more copies in this group"; return; }
        memberGroup = group; memberOffset = offset; Rows.Clear(); foreach (var file in result.Files) Rows.Add(new(group, file, scannedProfile, true));
        PageText = $"Group {group}: copies {offset + 1}–{offset + result.Files.Count}; planning considers this subset and always retains one keeper. Other copies remain untouched. Export includes all copies."; InvalidatePlan();
    }
    private IEnumerable<DuplicateGroup> DisplayedGroups() => Rows.GroupBy(r => r.GroupId).Select(g => new DuplicateGroup(g.Key, g.Select(r => r.File).ToArray(), "", g.Any(r => r.Partial)));
    private void Propose()
    {
        if (!fresh || scannedProfile is null) throw new IOException("A fresh completed scan is required.");
        var proposed = Planner.Create(DisplayedGroups(), EffectiveProfile(), Rule, Action, Quarantine, Preferred);
        var selected = proposed.Files.Select(f => f.Source.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in Rows) row.Selected = selected.Contains(row.File.Path); InvalidatePlan();
    }
    private void InvalidatePlan() { plan = null; PlanRows.Clear(); PlanText = "Build a plan from the current page's selections."; }
    private ScanProfile EffectiveProfile() => (scannedProfile ?? throw new IOException("Scan first.")) with { ProtectedDirectories = scannedProfile.ProtectedDirectories.Concat(Lines(Protections).Select(PathPolicy.Normalize)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() };
    private void BuildPlan()
    {
        if (!fresh || scannedProfile is null) throw new IOException("Rescan before planning historical results.");
        var profile = EffectiveProfile();
        var operations = new List<PlannedFile>(); var id = Guid.NewGuid();
        foreach (var group in Rows.GroupBy(r => r.GroupId))
        {
            var keeper = group.Where(r => !r.Selected).OrderBy(r => r.File.Path, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            if (keeper == null) throw new IOException($"Group {group.Key}: retain at least one keeper.");
            foreach (var row in group.Where(r => r.Selected))
            {
                if (row.File.ReviewRequired || PathPolicy.Protected(row.File.Path, profile)) throw new IOException("Protected or review-required files cannot be selected for cleanup.");
                string? destination = Action is CleanupAction.Quarantine or CleanupAction.Recycle ? Path.Combine(PathPolicy.Normalize(Quarantine), id.ToString("N"), Guid.NewGuid().ToString("N") + ".file") : null;
                operations.Add(new(row.File, keeper.File, Action, destination, $"User reviewed selection; keeper chosen by {Rule} or manual override."));
            }
        }
        plan = new(id, DateTime.UtcNow, operations, profile);
        PlanRows.Clear(); foreach (var operation in operations) PlanRows.Add(operation);
        PlanText = $"{operations.Count} files planned; {operations.Sum(o => o.Source.Length):N0} logical bytes. Quarantine and recycling may reclaim zero bytes. Recycling first stages the file in quarantine; if unavailable it remains there for explicit recovery. Every operation is revalidated."; Tab = 2;
    }
    private async Task ExecuteAsync()
    {
        if (plan is null || plan.Files.Count == 0) throw new IOException("Prepare a nonempty action plan first.");
        // Reject UI selections changed after plan review; execute only the displayed immutable plan.
        var selected = Rows.Where(r => r.Selected).Select(r => r.File.Path).Order().ToArray();
        if (!selected.SequenceEqual(plan.Files.Select(f => f.Source.Path).Order())) throw new IOException("Selections changed. Rebuild the plan.");
        if (!dialogs.ConfirmCleanup(plan)) return;
        cancellation = new();
        try { var executionPlan = plan with { Profile = plan.Profile with { ProtectedDirectories = plan.Profile.ProtectedDirectories.Concat(EffectiveProfile().ProtectedDirectories).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() } }; var progress = new Progress<string>(text => Status = text); bool permanent = PermanentEnabled; var result = await Task.Run(() => cleanup.ExecuteAsync(executionPlan, permanent, progress, cancellation.Token)); Details = string.Join(Environment.NewLine, result); Status = "Cleanup finished — inspect the journal for per-file outcomes"; }
        finally { fresh = false; cancellation.Dispose(); cancellation = null; InvalidatePlan(); await LoadHistoryAsync(); Tab = 3; }
    }
    private async Task RestoreAsync()
    {
        if (SelectedJournal is not { } entry) return;
        if (!dialogs.ConfirmRestore(entry)) return;
        cancellation = new();
        try { await Task.Run(() => cleanup.RestoreAsync(entry, cancellation.Token)); Status = "Quarantined file restored"; }
        finally { cancellation.Dispose(); cancellation = null; await LoadHistoryAsync(); }
    }
    private async Task ExportAsync()
    {
        if (scanId == null) return;
        var choice = dialogs.SaveExport();
        if (choice is null) return;
        if (File.Exists(choice.Value.Path)) throw new IOException("Choose a new export filename; exports never overwrite existing files.");
        cancellation = new();
        try { await Task.Run(() => ResultExporter.ExportAsync(store, scanId, choice.Value.Path, choice.Value.Json, cancellation.Token)); Status = "Results exported — paths are sensitive local information"; }
        finally { cancellation.Dispose(); cancellation = null; }
    }
    private async Task LoadHistoryAsync()
    {
        var history = await Task.Run(store.History); var journal = await Task.Run(cleanup.Journal);
        History.Clear(); foreach (var item in history) History.Add(new(item.Id, $"{item.Started}  |  {item.State}  |  {item.Id}"));
        Journal.Clear(); foreach (var item in journal) Journal.Add(item);
    }
}
