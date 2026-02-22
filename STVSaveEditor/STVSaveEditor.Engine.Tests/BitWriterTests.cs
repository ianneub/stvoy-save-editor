using STVSaveEditor.Engine;

namespace STVSaveEditor.Engine.Tests;

public class BitWriterTests
{
    [Fact]
    public void SetBit_SetsCorrectBit()
    {
        byte[] data = new byte[1];
        BitWriter.SetBit(data, 3, 1);
        Assert.Equal(0x08, data[0]);
    }

    [Fact]
    public void SetBit_ClearsBit()
    {
        byte[] data = new byte[] { 0xFF };
        BitWriter.SetBit(data, 0, 0);
        Assert.Equal(0xFE, data[0]);
    }

    [Fact]
    public void WriteBits_WritesValueLsbFirst()
    {
        byte[] data = new byte[2];
        BitWriter.WriteBits(data, 0, 0x05, 3);
        Assert.Equal(0x05, data[0]);
    }

    [Fact]
    public void GetBit_ReadsCorrectBit()
    {
        byte[] data = new byte[] { 0x08 };
        Assert.Equal(0, BitWriter.GetBit(data, 0));
        Assert.Equal(0, BitWriter.GetBit(data, 1));
        Assert.Equal(0, BitWriter.GetBit(data, 2));
        Assert.Equal(1, BitWriter.GetBit(data, 3));
    }

    [Fact]
    public void ReadBitsAt_ReadsCorrectValue()
    {
        byte[] data = new byte[] { 0xFF, 0x00 };
        Assert.Equal(0x0F, BitWriter.ReadBitsAt(data, 0, 4));
        Assert.Equal(0x00, BitWriter.ReadBitsAt(data, 8, 4));
    }

    [Fact]
    public void CopyBits_CopiesCorrectly()
    {
        byte[] src = new byte[] { 0xAB };
        byte[] dst = new byte[1];
        BitWriter.CopyBits(src, 0, dst, 0, 8);
        Assert.Equal(0xAB, dst[0]);
    }
}
