using STVSaveEditor.Engine;

namespace STVSaveEditor.Engine.Tests;

public class PackedEncodingTests
{
    [Fact]
    public void EncodeI32_Zero_SingleBit()
    {
        var bits = PackedEncoding.EncodeI32(0);
        Assert.Single(bits);
        Assert.Equal(0, bits[0]);
    }

    [Fact]
    public void EncodeI32_Positive_RoundTrips()
    {
        var bits = PackedEncoding.EncodeI32(5);
        byte[] data = BitsToBytes(bits, withDebugPrefix: true);
        var reader = new BitReader(data);
        Assert.Equal(5, reader.ReadI32Packed(-1));
    }

    [Fact]
    public void EncodeI32_Negative_RoundTrips()
    {
        var bits = PackedEncoding.EncodeI32(-42);
        byte[] data = BitsToBytes(bits, withDebugPrefix: true);
        var reader = new BitReader(data);
        Assert.Equal(-42, reader.ReadI32Packed(-1));
    }

    [Fact]
    public void EncodeU32_Zero_SingleBit()
    {
        var bits = PackedEncoding.EncodeU32(0);
        Assert.Single(bits);
        Assert.Equal(0, bits[0]);
    }

    [Fact]
    public void EncodeU32_Positive_RoundTrips()
    {
        var bits = PackedEncoding.EncodeU32(728);
        byte[] data = BitsToBytes(bits, withDebugPrefix: true);
        var reader = new BitReader(data);
        Assert.Equal(728u, reader.ReadU32Packed(-1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(127)]
    [InlineData(728)]
    [InlineData(99999)]
    [InlineData(int.MaxValue)]
    public void EncodeI32_RoundTrips_Various(int value)
    {
        var bits = PackedEncoding.EncodeI32(value);
        byte[] data = BitsToBytes(bits, withDebugPrefix: true);
        var reader = new BitReader(data);
        Assert.Equal(value, reader.ReadI32Packed(-1));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-42)]
    [InlineData(-99999)]
    public void EncodeI32_Negative_RoundTrips_Various(int value)
    {
        var bits = PackedEncoding.EncodeI32(value);
        byte[] data = BitsToBytes(bits, withDebugPrefix: true);
        var reader = new BitReader(data);
        Assert.Equal(value, reader.ReadI32Packed(-1));
    }

    private static byte[] BitsToBytes(List<int> bits, bool withDebugPrefix)
    {
        var allBits = new List<int>();
        if (withDebugPrefix)
            allBits.Add(0);
        allBits.AddRange(bits);

        int byteCount = (allBits.Count + 7) / 8;
        byte[] result = new byte[byteCount];
        for (int i = 0; i < allBits.Count; i++)
        {
            if (allBits[i] != 0)
                result[i >> 3] |= (byte)(1 << (i & 7));
        }
        return result;
    }
}
