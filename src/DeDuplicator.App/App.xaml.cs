using System.Windows;
using DeDuplicator.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeDuplicator.App;
public partial class App : Application
{
    private IHost? host;
    private Mutex? instance;
    private ApplicationDataLease? dataLease;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instance = new Mutex(true, "Local\\DeDuplicator-" + Environment.UserName, out bool created);
        if (!created) { MessageBox.Show("DeDuplicator is already running. Use the existing window to protect active scans and cleanup operations.", "DeDuplicator"); Shutdown(); return; }
        try
        {
        string data = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeDuplicator");
        dataLease = new ApplicationDataLease(data);
        host = Host.CreateDefaultBuilder().ConfigureLogging(builder => builder.ClearProviders().AddDebug()).ConfigureServices(services =>
        {
            services.AddSingleton(new ScanStore(System.IO.Path.Combine(data, "scans.db")));
            services.AddSingleton<IContentHasher, Sha256Hasher>(); services.AddTransient<Scanner>(); services.AddSingleton<CleanupService>(); services.AddSingleton<MainViewModel>(); services.AddSingleton<MainWindow>();
        }).Build();
        host.Services.GetRequiredService<ScanStore>().Recover();
        host.Services.GetRequiredService<MainWindow>().Show();
        }
        catch (Exception ex) { MessageBox.Show("DeDuplicator could not start. No cleanup was performed.\n\n" + ex.Message, "DeDuplicator", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(1); }
    }
    protected override void OnExit(ExitEventArgs e) { host?.Dispose(); dataLease?.Dispose(); instance?.Dispose(); base.OnExit(e); }
}
