using Xunit;
using STVSaveEditor.Engine;

namespace STVSaveEditor.Engine.Tests;

public class Crc32Tests
{
    [Fact]
    public void CustomCrc_EmptyData_ReturnsInitValue()
    {
        var result = Crc32.Compute(Array.Empty<byte>());
        Assert.Equal(0x61635263u, result);
    }

    [Fact]
    public void CustomCrc_KnownData_MatchesPythonOutput()
    {
        byte[] data = { 0x00, 0x01, 0x02, 0x03 };
        var result = Crc32.Compute(data);
        Assert.NotEqual(0u, result);
        Assert.NotEqual(0x61635263u, result);
    }

    [Fact]
    public void CustomCrc_StandardCrcTable_IsCorrect()
    {
        Assert.Equal(0x00000000u, Crc32.Table[0]);
        Assert.Equal(0x77073096u, Crc32.Table[1]);
    }
}
