using System.Windows;
using Microsoft.Win32;
using STVSaveEditor.ViewModels;

namespace STVSaveEditor;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select a save file",
            Filter = "Save files (*.sav)|*.sav|All files (*.*)|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        };

        if (dialog.ShowDialog() == true)
        {
            var vm = (MainViewModel)DataContext;
            string folder = System.IO.Path.GetDirectoryName(dialog.FileName)!;
            vm.SaveFolder = folder;
            vm.SelectedFile = dialog.FileName;
        }
    }
}
