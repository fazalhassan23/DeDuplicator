using System.Windows;
namespace DeDuplicator.App;
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel model)
    {
        InitializeComponent(); DataContext = model;
        Closing += (_, e) => { if (model.Busy) { model.Cancel.Execute(null); e.Cancel = true; MessageBox.Show(this, "Cancellation requested. Wait for the operation to stop before closing.", "DeDuplicator"); } };
    }
}
