using STVSaveEditor.Engine;

namespace STVSaveEditor.Engine.Tests;

public class BitReaderTests
{
    [Fact]
    public void ReadBool_ReadsLsbFirst()
    {
        var reader = new BitReader(new byte[] { 0xB1 });
        Assert.Equal(1, reader.ReadBool());
        Assert.Equal(0, reader.ReadBool());
        Assert.Equal(0, reader.ReadBool());
        Assert.Equal(0, reader.ReadBool());
        Assert.Equal(1, reader.ReadBool());
        Assert.Equal(1, reader.ReadBool());
        Assert.Equal(0, reader.ReadBool());
        Assert.Equal(1, reader.ReadBool());
    }

    [Fact]
    public void ReadBits_ReadsMultipleBitsLsbFirst()
    {
        var reader = new BitReader(new byte[] { 0xFF });
        Assert.Equal(15, reader.ReadBits(4));
    }

    [Fact]
    public void ReadBits_CrossesByteBoundary()
    {
        var reader = new BitReader(new byte[] { 0x0F, 0xF0 }, pos: 4);
        Assert.Equal(0xFF, reader.ReadBits(8));
    }

    [Fact]
    public void ReadU32Packed_Zero_ReturnsZero()
    {
        var reader = new BitReader(new byte[] { 0x00 });
        Assert.Equal(0u, reader.ReadU32Packed(-1));
    }

    [Fact]
    public void ReadI32Packed_Zero_ReturnsZero()
    {
        var reader = new BitReader(new byte[] { 0x00 });
        Assert.Equal(0, reader.ReadI32Packed(-1));
    }

    [Fact]
    public void ReadI32Packed_PositiveValue()
    {
        byte[] data = BuildBitsAsBytes(0, 1, 0, 1, 0, 1, 1, 1, 1, 0, 1);
        var reader = new BitReader(data);
        Assert.Equal(5, reader.ReadI32Packed(-1));
    }

    [Fact]
    public void ReadI32Packed_NegativeValue()
    {
        byte[] data = BuildBitsAsBytes(0, 1, 1, 1, 0, 1, 1, 1, 1, 0, 1);
        var reader = new BitReader(data);
        Assert.Equal(-5, reader.ReadI32Packed(-1));
    }

    [Fact]
    public void ReadString_EmptyString()
    {
        byte[] data = BuildBitsAsBytes(0, 0);
        var reader = new BitReader(data);
        Assert.Equal("", reader.ReadString(-1));
    }

    private static byte[] BuildBitsAsBytes(params int[] bits)
    {
        int byteCount = (bits.Length + 7) / 8;
        byte[] result = new byte[byteCount];
        for (int i = 0; i < bits.Length; i++)
        {
            if (bits[i] != 0)
                result[i >> 3] |= (byte)(1 << (i & 7));
        }
        return result;
    }
}
