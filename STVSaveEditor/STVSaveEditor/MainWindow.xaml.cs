using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using STVSaveEditor.ViewModels;

namespace STVSaveEditor;

public partial class MainWindow : Window
{
    private bool _miscExpanded = false;

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

    private void MiscHeader_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _miscExpanded = !_miscExpanded;
        MiscItems.Visibility = _miscExpanded ? Visibility.Visible : Visibility.Collapsed;
        MiscArrow.RenderTransform = _miscExpanded
            ? new RotateTransform(180, 4, 2)
            : null;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) ToggleMaximize();
        else DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    
    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
}
