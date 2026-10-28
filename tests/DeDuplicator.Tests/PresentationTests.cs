using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeDuplicator.App;
using DeDuplicator.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeDuplicator.Tests;

public sealed class PresentationTests
{
    [Fact]
    public async Task WpfResourcesAndWorkflowViewsRenderOnStaThread()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            string fixture = Path.Combine(Path.GetTempPath(), "DeDuplicatorTests", Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(fixture);
                var app = new DeDuplicator.App.App(); app.InitializeComponent();
                var store = new ScanStore(Path.Combine(fixture, "ui.db"));
                string input = Path.Combine(fixture, "source"); Directory.CreateDirectory(input);
                File.WriteAllText(Path.Combine(input, "original.txt"), "WPF test fixture contents"); File.WriteAllText(Path.Combine(input, "copy.txt"), "WPF test fixture contents");
                var profile = new DeDuplicator.Core.ScanProfile { Roots = [input] };
                string scan = Task.Run(() => new Scanner(store, new Sha256Hasher()).ScanAsync(profile, null, default)).GetAwaiter().GetResult();
                var model = new MainViewModel(store, new Scanner(store, new Sha256Hasher()), new CleanupService(store), NullLogger<MainViewModel>.Instance);
                model.Roots = input;
                foreach (var group in store.Groups(scan)) foreach (var file in group.Files) model.Rows.Add(new(group.Id, file, profile));
                foreach (var operation in DeDuplicator.Core.Planner.Create(store.Groups(scan), profile, DeDuplicator.Core.KeepRule.ShortestPath, DeDuplicator.Core.CleanupAction.Quarantine, Path.Combine(fixture, "quarantine")).Files) model.PlanRows.Add(operation);
                var window = new MainWindow(model);
                var content = (FrameworkElement)window.Content;
                string artifacts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/ui")); Directory.CreateDirectory(artifacts);
                foreach (int tab in new[] { 0, 1, 2, 3 })
                {
                    model.Tab = tab;
                    content.Measure(new Size(1320, 820)); content.Arrange(new Rect(0, 0, 1320, 820)); content.UpdateLayout();
                    Assert.True(content.ActualWidth > 0);
                    var bitmap = new RenderTargetBitmap(1320, 820, 96, 96, PixelFormats.Pbgra32); bitmap.Render(content);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var output = File.Create(Path.Combine(artifacts, $"workflow-{tab}.png")); encoder.Save(output);
                }
                window.Close(); app.Shutdown(); completion.SetResult();
            }
            catch (Exception ex) { completion.SetException(ex); }
            finally
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (Directory.Exists(fixture)) Directory.Delete(fixture, true);
            }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
}
