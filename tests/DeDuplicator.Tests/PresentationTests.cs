using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeDuplicator.App;
using DeDuplicator.Core;
using DeDuplicator.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeDuplicator.Tests;

public sealed class PresentationTests
{
    private sealed class FixtureDialogs(string root, string input) : IUserDialogs
    {
        public bool Confirm { get; set; }
        public string ProfilePath { get; set; } = Path.Combine(root, "profile.json");
        public string[] ChooseFolders() => [input];
        public string? OpenProfile() => ProfilePath;
        public string? SaveProfile() => ProfilePath;
        public (string Path, bool Json)? SaveExport() => (Path.Combine(root, "export.json"), true);
        public bool ConfirmCleanup(ActionPlan plan) => Confirm;
        public bool ConfirmRestore(JournalEntry entry) => Confirm;
        public bool ConfirmOpen(string path) => false;
    }

    [Fact]
    public async Task NativeWpfWindowRunsScanReviewConfirmationQuarantineRestoreExportAndCancellation()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            string fixture = Path.Combine(Path.GetTempPath(), "DeDuplicatorPresentation", Guid.NewGuid().ToString("N"));
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/DeDuplicator.App;component/Resources/Theme.xaml", UriKind.Relative) });
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            app.Dispatcher.BeginInvoke(new Action(async () =>
            {
                MainWindow? window = null;
                try
                {
                    string input = Path.Combine(fixture, "input"); Directory.CreateDirectory(input);
                    File.WriteAllText(Path.Combine(input, "original.txt"), "actual fixture bytes"); File.WriteAllText(Path.Combine(input, "copy.txt"), "actual fixture bytes");
                    var store = new ScanStore(Path.Combine(fixture, "state.db"));
                    var dialogs = new FixtureDialogs(fixture, input);
                    var model = new MainViewModel(store, new Scanner(store, new Sha256Hasher()), new CleanupService(store), NullLogger<MainViewModel>.Instance, dialogs) { Quarantine = Path.Combine(fixture, "quarantine") };
                    await model.Initialization;
                    window = new MainWindow(model) { ShowActivated = false, ShowInTaskbar = false, Opacity = 0 };
                    window.Show(); window.UpdateLayout();
                    Assert.True(window.IsLoaded); Assert.NotNull(PresentationSource.FromVisual(window));
                    model.AddFolder.Execute(null);
                    await ((AsyncCommand)model.Scan).ExecuteAsync();
                    Assert.Equal(2, model.Rows.Count); Assert.Equal("Complete", model.Status); Assert.False(model.Busy);
                    Assert.All(model.Rows, row => Assert.Equal("Verified at scan", row.Safety));
                    model.SelectRule.Execute(null); Assert.Single(model.Rows, row => row.Selected);
                    model.Review.Execute(null); var operation = Assert.Single(model.PlanRows);
                    await ((AsyncCommand)model.Execute).ExecuteAsync();
                    Assert.True(File.Exists(operation.Source.Path)); Assert.Empty(model.Journal);
                    dialogs.Confirm = true;
                    bool busyDuringHistory = true;
                    model.Journal.CollectionChanged += (_, _) => busyDuringHistory &= model.Busy;
                    await ((AsyncCommand)model.Execute).ExecuteAsync();
                    Assert.True(busyDuringHistory); Assert.False(model.Busy);
                    Assert.False(File.Exists(operation.Source.Path)); Assert.True(File.Exists(operation.Destination));
                    model.SelectedJournal = Assert.Single(model.Journal);
                    await ((AsyncCommand)model.Restore).ExecuteAsync(); Assert.Equal("actual fixture bytes", File.ReadAllText(operation.Source.Path));
                    await ((AsyncCommand)model.Export).ExecuteAsync();
                    using (var exported = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture, "export.json")))) Assert.Equal(2, exported.RootElement.GetArrayLength());
                    await ((AsyncCommand)model.SaveProfile).ExecuteAsync();
                    model.Roots = ""; await ((AsyncCommand)model.LoadProfile).ExecuteAsync(); Assert.Equal(input, model.Roots);
                    File.WriteAllText(dialogs.ProfilePath, "{\"Roots\":null}"); await ((AsyncCommand)model.LoadProfile).ExecuteAsync(); Assert.Contains("arrays", model.Details);
                    model.SelectedHistory = model.History.First(row => row.Label.Contains("Complete"));
                    await ((AsyncCommand)model.ViewHistory).ExecuteAsync(); Assert.Contains("Historical", model.PageText);
                    model.SelectRule.Execute(null); Assert.Contains("fresh completed scan", model.Details);
                    var running = ((AsyncCommand)model.Scan).ExecuteAsync(); Assert.True(model.Busy);
                    model.Cancel.Execute(null); await running; Assert.Contains("cancel", model.Status, StringComparison.OrdinalIgnoreCase); Assert.False(model.Busy);
                    string artifacts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/ui")); Directory.CreateDirectory(artifacts);
                    foreach (int tab in new[] { 0, 1, 2, 3 })
                    {
                        model.Tab = tab;
                        var content = (FrameworkElement)window.Content;
                        content.Measure(new Size(1320, 820)); content.Arrange(new Rect(0, 0, 1320, 820)); content.UpdateLayout();
                        var bitmap = new RenderTargetBitmap(1320, 820, 96, 96, PixelFormats.Pbgra32); bitmap.Render(content);
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var output = File.Create(Path.Combine(artifacts, $"workflow-{tab}.png")); encoder.Save(output);
                    }
                    completion.SetResult();
                }
                catch (Exception error) { completion.SetException(error); }
                finally
                {
                    window?.Close(); app.Shutdown();
                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                    string prefix = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "DeDuplicatorPresentation")) + Path.DirectorySeparatorChar;
                    if (Path.GetFullPath(fixture).StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fixture)) Directory.Delete(fixture, true);
                }
            }));
            app.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(60));
    }
}
