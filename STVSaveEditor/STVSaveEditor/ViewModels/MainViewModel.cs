using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using STVSaveEditor.Engine;

namespace STVSaveEditor.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private string _saveFolder = "";
    private string _selectedFile = "";
    private string _statusMessage = "Ready";
    private float _hullIntegrity;
    private float _hullMax = 490f;
    private int _currentMorale;
    private int _originalMorale;
    private int _moraleMax;
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
        set { _statusMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusBrush)); }
    }

    public Brush StatusBrush
    {
        get
        {
            if (_statusMessage.StartsWith("Error"))
                return new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));
            if (_statusMessage.StartsWith("Saved"))
                return new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A));
            return new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xDD));
        }
    }

    public float HullIntegrity
    {
        get => _hullIntegrity;
        set { _hullIntegrity = value; OnPropertyChanged(); }
    }

    public int CurrentMorale
    {
        get => _currentMorale;
        set 
        { 
            _currentMorale = Math.Min(value, _moraleMax); 
            OnPropertyChanged(); 
        }
    }

    public int MoraleMax
    {
        get => _moraleMax;
        set { _moraleMax = value; OnPropertyChanged(); }
    }

    public float HullMax
    {
        get => _hullMax;
        set { _hullMax = value; OnPropertyChanged(); }
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

            var excludedResources = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Energy", "Cycles", "CrewAssigned", "Happiness", "LivingSpace",
                "WorkTeams", "WorkTeamsAssigned", "Batteries", "Hull", "ThreatLevel",
                "Morale", "MoraleMax"
            };

            foreach (var r in resources)
            {
                if (r.Name == "Morale")
                {
                    _currentMorale = r.Quantity;
                    _originalMorale = r.Quantity;
                    OnPropertyChanged(nameof(CurrentMorale));
                    continue;
                }
                if (r.Name == "MoraleMax")
                {
                    _moraleMax = r.Quantity;
                    OnPropertyChanged(nameof(MoraleMax));
                    continue;
                }

                if (!r.IsItem && excludedResources.Contains(r.Name))
                    continue;

                bool isTorpedo = r.Name == "Items.Item.Item_Torpedo";
                string displayName = isTorpedo ? "TORPEDOES" : (r.IsItem ? r.Name.Replace("Items.Item.", "") : r.Name);

                // Format specific names
                if (displayName.Equals("BioNeuralGelPack", StringComparison.OrdinalIgnoreCase)) displayName = "BIO-NEURAL GEL PACK";
                else if (displayName.Equals("SciencePoints", StringComparison.OrdinalIgnoreCase)) displayName = "SCIENCE POINTS";
                else if (displayName.Equals("BorgNanites", StringComparison.OrdinalIgnoreCase)) displayName = "BORG NANITES";
                
                displayName = displayName.ToUpperInvariant();

                bool isActuallyItem = r.IsItem && !isTorpedo;

                var vm = new ResourceViewModel
                {
                    Name = r.Name,
                    DisplayName = displayName,
                    Quantity = r.Quantity,
                    NewQuantity = r.Quantity,
                    IsItem = isActuallyItem,
                };

                if (r.Name == "Crew")
                {
                    vm.ToolTipText = "CREW COUNT IS YOUR TOTAL ACTIVE CREW MINUS YOUR HEROES. DO NOT INCREASE THIS NUMBER + HERO COUNT BEYOND YOUR CURRENT MAX CREW CAPACITY IN YOUR SAVE.";
                }

                if (isActuallyItem)
                    Items.Add(vm);
                else
                    BaseResources.Add(vm);
            }

            var hull = ChunkNavigator.ReadHullIntegrity(_currentData);
            HullIntegrity = hull.Value;

            StatusMessage = $"LOADED: {Path.GetFileName(SelectedFile).ToUpperInvariant()} ({resources.Count} RESOURCES)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"ERROR LOADING: {ex.Message.ToUpperInvariant()}";
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
            StatusMessage = "NO FILE LOADED";
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

            if (CurrentMorale != _originalMorale)
            {
                mods["Morale"] = CurrentMorale;
            }

            byte[] data = _currentData;

            if (mods.Count > 0)
                data = ChunkNavigator.ModifyResources(data, mods);

            var currentHull = ChunkNavigator.ReadHullIntegrity(data);
            if (Math.Abs(currentHull.Value - HullIntegrity) > 0.001f)
                ChunkNavigator.SetHullIntegrity(data, HullIntegrity);

            SaveFile.Save(data, SelectedFile);
            StatusMessage = $"SAVED SUCCESSFULLY! ({mods.Count} RESOURCES MODIFIED)";

            _currentData = data;
            LoadSaveFile();
        }
        catch (Exception ex)
        {
            StatusMessage = $"ERROR SAVING: {ex.Message.ToUpperInvariant()}";
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
        set
        {
            _newQuantity = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NewQuantity)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsModified)));
        }
    }
    public bool IsModified => NewQuantity != Quantity;
    public bool IsItem { get; set; }
    public string ToolTipText { get; set; } = "";
    public bool HasToolTip => !string.IsNullOrEmpty(ToolTipText);

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class FilePathToNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string path ? Path.GetFileName(path) : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    public RelayCommand(Action execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute();
}
