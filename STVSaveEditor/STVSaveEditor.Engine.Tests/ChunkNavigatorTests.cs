using Xunit;
using STVSaveEditor.Engine;

namespace STVSaveEditor.Engine.Tests;

public class ChunkNavigatorTests
{
    private static readonly string TestSavePath = FindTestSave();

    private static string FindTestSave()
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            string candidate = Path.Combine(dir, "original", "00_GX_STV_SaveGame_0000.sav");
            if (File.Exists(candidate)) return candidate;
            candidate = Path.Combine(dir, "..", "..", "..", "..", "..", "original", "00_GX_STV_SaveGame_0000.sav");
            candidate = Path.GetFullPath(candidate);
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir)!;
        }
        return "";
    }

    [SkippableFact]
    public void ReadResources_ParsesRealSaveFile()
    {
        Skip.If(string.IsNullOrEmpty(TestSavePath) || !File.Exists(TestSavePath),
            "Test save file not found");

        byte[] data = SaveFile.Load(TestSavePath);
        var resources = ChunkNavigator.ReadResources(data);

        Assert.NotEmpty(resources);
        Assert.Contains(resources, r => r.Name == "Crew");
        Assert.Contains(resources, r => r.Name == "Energy");
        Assert.Contains(resources, r => r.Name == "Deuterium");
        var crew = resources.First(r => r.Name == "Crew");
        Assert.False(crew.IsItem);
    }

    [SkippableFact]
    public void ReadHullIntegrity_ParsesRealSaveFile()
    {
        Skip.If(string.IsNullOrEmpty(TestSavePath) || !File.Exists(TestSavePath),
            "Test save file not found");

        byte[] data = SaveFile.Load(TestSavePath);
        var hull = ChunkNavigator.ReadHullIntegrity(data);

        Assert.True(hull.Value > 0, "Hull should be positive");
        Assert.True(hull.BitPos > 0, "Bit position should be positive");
    }

    [SkippableFact]
    public void GetChunkSizePositions_ReturnsFourPositions()
    {
        Skip.If(string.IsNullOrEmpty(TestSavePath) || !File.Exists(TestSavePath),
            "Test save file not found");

        byte[] data = SaveFile.Load(TestSavePath);
        var positions = ChunkNavigator.GetChunkSizePositions(data);

        Assert.Equal(4, positions.Count);
        Assert.All(positions, p => Assert.True(p > 0));
    }
}
