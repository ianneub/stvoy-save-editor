using System.Windows;

namespace STVSaveEditor;

public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var warning = new WarningDialog();
        if (warning.ShowDialog() == true)
        {
            var main = new MainWindow();
            main.Show();
        }
        else
        {
            Shutdown();
        }
    }
}
