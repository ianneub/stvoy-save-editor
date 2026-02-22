namespace STVSaveEditor.Engine;

public static class SaveFolderDetector
{
    private static readonly string[] CommonSteamPaths = new[]
    {
        @"C:\Program Files (x86)\Steam",
        @"C:\Program Files\Steam",
        @"D:\Steam",
        @"D:\SteamLibrary",
        @"E:\SteamLibrary",
    };

    public static List<string> FindSaveFiles(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return new List<string>();

        return Directory.GetFiles(folderPath, "*.sav")
            .OrderBy(f => f)
            .ToList();
    }

    public static string? AutoDetectSaveFolder()
    {
        foreach (string steamPath in CommonSteamPaths)
        {
            string? result = SearchSteamPath(steamPath);
            if (result != null) return result;
        }

        foreach (string steamPath in CommonSteamPaths)
        {
            string vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdfPath)) continue;

            foreach (string libraryPath in ParseLibraryFolders(vdfPath))
            {
                string? result = SearchSteamPath(libraryPath);
                if (result != null) return result;
            }
        }

        return null;
    }

    private static string? SearchSteamPath(string steamPath)
    {
        string saveBase = Path.Combine(steamPath, "steamapps", "common",
            "Star Trek Voyager - Across the Unknown", "STVoyager", "Saved", "SaveGames");

        if (!Directory.Exists(saveBase)) return null;

        foreach (string userDir in Directory.GetDirectories(saveBase))
        {
            if (Directory.GetFiles(userDir, "*.sav").Length > 0)
                return userDir;
        }

        return null;
    }

    public static string? AutoDetectLocalAppData()
    {
        string? localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(localAppData)) return null;

        string saveBase = Path.Combine(localAppData, "STVoyager", "Saved", "SaveGames");
        if (!Directory.Exists(saveBase)) return null;

        foreach (string userDir in Directory.GetDirectories(saveBase))
        {
            if (Directory.GetFiles(userDir, "*.sav").Length > 0)
                return userDir;
        }

        return null;
    }

    private static List<string> ParseLibraryFolders(string vdfPath)
    {
        var paths = new List<string>();
        try
        {
            string content = File.ReadAllText(vdfPath);
            foreach (string line in content.Split('\n'))
            {
                string trimmed = line.Trim().Trim('"');
                if (trimmed.StartsWith("path"))
                {
                    string[] parts = line.Trim().Split('"');
                    if (parts.Length >= 4)
                        paths.Add(parts[3].Replace(@"\\", @"\"));
                }
            }
        }
        catch { /* ignore parse errors */ }
        return paths;
    }
}
