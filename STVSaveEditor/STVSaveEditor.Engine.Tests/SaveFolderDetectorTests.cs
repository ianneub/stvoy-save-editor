using Xunit;
using STVSaveEditor.Engine;

namespace STVSaveEditor.Engine.Tests;

public class SaveFolderDetectorTests
{
    [Fact]
    public void FindSaveFiles_ReturnsEmptyForNonexistentPath()
    {
        var files = SaveFolderDetector.FindSaveFiles(@"C:\nonexistent\path");
        Assert.Empty(files);
    }

    [Fact]
    public void FindSaveFiles_FindsSavFilesInDirectory()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "stvoy_detect_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "00_GX_STV_SaveGame_0000.sav"), "test");
            File.WriteAllText(Path.Combine(tempDir, "00_GX_STV_SaveGame_0001.sav"), "test");
            File.WriteAllText(Path.Combine(tempDir, "other.txt"), "not a save");

            var files = SaveFolderDetector.FindSaveFiles(tempDir);
            Assert.Equal(2, files.Count);
            Assert.All(files, f => Assert.EndsWith(".sav", f));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
