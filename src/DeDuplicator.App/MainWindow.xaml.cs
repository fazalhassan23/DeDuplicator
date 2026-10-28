using System.Windows;
using System.Reflection;
namespace DeDuplicator.App;
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel model)
    {
        InitializeComponent(); DataContext = model;
        Title = "DeDuplicator · " + typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        Closing += (_, e) => { if (model.Busy) { model.Cancel.Execute(null); e.Cancel = true; MessageBox.Show(this, "Cancellation requested. Wait for the operation to stop before closing.", "DeDuplicator"); } };
    }
}
