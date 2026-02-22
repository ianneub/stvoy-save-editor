using STVSaveEditor.Engine;

namespace STVSaveEditor.Engine.Tests;

public class IntegrationTests : IDisposable
{
    private readonly string _tempDir;

    public IntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "stvoy_int_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private static string? FindTestSave()
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            string candidate = Path.Combine(dir, "..", "..", "..", "..", "..", "original", "00_GX_STV_SaveGame_0000.sav");
            candidate = Path.GetFullPath(candidate);
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir)!;
        }
        return null;
    }

    [SkippableFact]
    public void ModifyResource_RoundTrip_PreservesOtherValues()
    {
        var savePath = FindTestSave();
        Skip.If(savePath == null, "Test save not found");

        byte[] original = SaveFile.Load(savePath!);
        var originalResources = ChunkNavigator.ReadResources(original);

        byte[] modified = ChunkNavigator.ModifyResources(original, new Dictionary<string, int>
        {
            { "Energy", 99999 }
        });

        string tempPath = Path.Combine(_tempDir, "modified.sav");
        SaveFile.Save(modified, tempPath);
        byte[] reloaded = SaveFile.Load(tempPath);

        var newResources = ChunkNavigator.ReadResources(reloaded);
        var energy = newResources.First(r => r.Name == "Energy");
        Assert.Equal(99999, energy.Quantity);

        foreach (var orig in originalResources.Where(r => r.Name != "Energy"))
        {
            var updated = newResources.First(r => r.Name == orig.Name);
            Assert.Equal(orig.Quantity, updated.Quantity);
        }
    }

    [SkippableFact]
    public void SetHull_RoundTrip()
    {
        var savePath = FindTestSave();
        Skip.If(savePath == null, "Test save not found");

        byte[] data = SaveFile.Load(savePath!);

        ChunkNavigator.SetHullIntegrity(data, 490.0f);

        string tempPath = Path.Combine(_tempDir, "hull_mod.sav");
        SaveFile.Save(data, tempPath);
        byte[] reloaded = SaveFile.Load(tempPath);

        var newHull = ChunkNavigator.ReadHullIntegrity(reloaded);
        Assert.Equal(490.0f, newHull.Value);
    }
}
