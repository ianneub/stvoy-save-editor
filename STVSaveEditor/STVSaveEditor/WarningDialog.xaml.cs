using System.Windows;

namespace STVSaveEditor;

public partial class WarningDialog : Window
{
    public WarningDialog()
    {
        InitializeComponent();
    }

    private void Understand_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Quit_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
