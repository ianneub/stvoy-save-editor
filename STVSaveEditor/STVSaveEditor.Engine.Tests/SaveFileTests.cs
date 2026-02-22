using STVSaveEditor.Engine;

namespace STVSaveEditor.Engine.Tests;

public class SaveFileTests : IDisposable
{
    private readonly string _tempDir;

    public SaveFileTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "stvoy_tests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Load_DecodesBase64AndValidatesSize()
    {
        byte[] payload = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
        byte[] raw = new byte[16 + payload.Length];
        BitConverter.GetBytes((uint)raw.Length).CopyTo(raw, 0);
        BitConverter.GetBytes(0u).CopyTo(raw, 4);
        BitConverter.GetBytes(1u).CopyTo(raw, 8);
        BitConverter.GetBytes(1u).CopyTo(raw, 12);
        Array.Copy(payload, 0, raw, 16, payload.Length);
        uint hash = Crc32.Compute(raw.AsSpan(16));
        BitConverter.GetBytes(hash).CopyTo(raw, 4);

        string path = Path.Combine(_tempDir, "test.sav");
        File.WriteAllBytes(path, System.Text.Encoding.ASCII.GetBytes(Convert.ToBase64String(raw)));

        byte[] loaded = SaveFile.Load(path);
        Assert.Equal(raw.Length, loaded.Length);
        Assert.Equal(raw, loaded);
    }

    [Fact]
    public void Save_RecomputesCrcAndEncodesBase64()
    {
        byte[] data = new byte[20];
        BitConverter.GetBytes((uint)20).CopyTo(data, 0);
        BitConverter.GetBytes(1u).CopyTo(data, 8);
        BitConverter.GetBytes(1u).CopyTo(data, 12);
        data[16] = 0x42;

        string path = Path.Combine(_tempDir, "output.sav");
        SaveFile.Save(data, path);

        byte[] loaded = SaveFile.Load(path);
        uint storedHash = BitConverter.ToUInt32(loaded, 4);
        uint computedHash = Crc32.Compute(loaded.AsSpan(16));
        Assert.Equal(computedHash, storedHash);
    }

    [Fact]
    public void MakeBackup_CreatesBackupOnce()
    {
        string path = Path.Combine(_tempDir, "save.sav");
        File.WriteAllText(path, "original");

        Assert.True(SaveFile.MakeBackup(path));
        Assert.True(File.Exists(path + ".backup"));
        Assert.Equal("original", File.ReadAllText(path + ".backup"));

        File.WriteAllText(path, "modified");
        Assert.False(SaveFile.MakeBackup(path));
        Assert.Equal("original", File.ReadAllText(path + ".backup"));
    }
}
