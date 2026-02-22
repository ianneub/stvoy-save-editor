using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using STVSaveEditor.Engine;

namespace STVSaveEditor.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private string _saveFolder = "";
    private string _selectedFile = "";
    private string _statusMessage = "Ready";
    private float _hullIntegrity;
    private byte[]? _currentData;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string SaveFolder
    {
        get => _saveFolder;
        set { _saveFolder = value; OnPropertyChanged(); LoadFileList(); }
    }

    public ObservableCollection<string> SaveFiles { get; } = new();

    public string SelectedFile
    {
        get => _selectedFile;
        set { _selectedFile = value; OnPropertyChanged(); if (!string.IsNullOrEmpty(value)) LoadSaveFile(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public float HullIntegrity
    {
        get => _hullIntegrity;
        set { _hullIntegrity = value; OnPropertyChanged(); }
    }

    public ObservableCollection<ResourceViewModel> BaseResources { get; } = new();
    public ObservableCollection<ResourceViewModel> Items { get; } = new();

    public ICommand BrowseCommand => new RelayCommand(Browse);
    public ICommand SaveCommand => new RelayCommand(Save);
    public ICommand ReloadCommand => new RelayCommand(Reload);

    public MainViewModel()
    {
        AutoDetect();
    }

    private void AutoDetect()
    {
        string? folder = SaveFolderDetector.AutoDetectLocalAppData()
                      ?? SaveFolderDetector.AutoDetectSaveFolder();
        if (folder != null)
            SaveFolder = folder;
    }

    private void LoadFileList()
    {
        SaveFiles.Clear();
        var files = SaveFolderDetector.FindSaveFiles(SaveFolder);
        foreach (string f in files)
            SaveFiles.Add(f);
        if (SaveFiles.Count > 0)
            SelectedFile = SaveFiles[0];
    }

    private void LoadSaveFile()
    {
        try
        {
            _currentData = SaveFile.Load(SelectedFile);

            var resources = ChunkNavigator.ReadResources(_currentData);
            BaseResources.Clear();
            Items.Clear();
            foreach (var r in resources)
            {
                var vm = new ResourceViewModel
                {
                    Name = r.Name,
                    DisplayName = r.IsItem ? r.Name.Replace("Items.Item.", "") : r.Name,
                    Quantity = r.Quantity,
                    NewQuantity = r.Quantity,
                    IsItem = r.IsItem,
                };
                if (r.IsItem)
                    Items.Add(vm);
                else
                    BaseResources.Add(vm);
            }

            var hull = ChunkNavigator.ReadHullIntegrity(_currentData);
            HullIntegrity = hull.Value;

            StatusMessage = $"Loaded: {Path.GetFileName(SelectedFile)} ({resources.Count} resources)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading: {ex.Message}";
        }
    }

    private void Browse()
    {
        // Wired to OpenFileDialog in MainWindow.xaml.cs
    }

    private void Reload()
    {
        if (!string.IsNullOrEmpty(SelectedFile))
            LoadSaveFile();
    }

    private void Save()
    {
        if (_currentData == null || string.IsNullOrEmpty(SelectedFile))
        {
            StatusMessage = "No file loaded";
            return;
        }

        try
        {
            SaveFile.MakeBackup(SelectedFile);

            var mods = new Dictionary<string, int>();
            foreach (var r in BaseResources.Concat(Items))
            {
                if (r.NewQuantity != r.Quantity)
                    mods[r.Name] = r.NewQuantity;
            }

            byte[] data = _currentData;

            if (mods.Count > 0)
                data = ChunkNavigator.ModifyResources(data, mods);

            var currentHull = ChunkNavigator.ReadHullIntegrity(data);
            if (Math.Abs(currentHull.Value - HullIntegrity) > 0.001f)
                ChunkNavigator.SetHullIntegrity(data, HullIntegrity);

            SaveFile.Save(data, SelectedFile);
            StatusMessage = $"Saved successfully! ({mods.Count} resources modified)";

            _currentData = data;
            LoadSaveFile();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving: {ex.Message}";
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class ResourceViewModel : INotifyPropertyChanged
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Quantity { get; set; }
    private int _newQuantity;
    public int NewQuantity
    {
        get => _newQuantity;
        set { _newQuantity = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NewQuantity))); }
    }
    public bool IsItem { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    public RelayCommand(Action execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute();
}
