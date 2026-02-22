namespace STVSaveEditor.Engine;

public static class PackedEncoding
{
    public static List<int> EncodeI32(int value)
    {
        var bits = new List<int>();
        if (value == 0)
        {
            bits.Add(0);
            return bits;
        }
        bits.Add(1);
        bits.Add(value < 0 ? 1 : 0);
        int magnitude = Math.Abs(value);
        int lzc = 0;
        for (int b = 31; b >= 0; b--)
        {
            if ((magnitude & (1 << b)) != 0) break;
            lzc++;
        }
        for (int i = 0; i < 5; i++)
            bits.Add((lzc >> i) & 1);
        int vb = 32 - lzc;
        for (int i = 0; i < vb; i++)
            bits.Add((magnitude >> i) & 1);
        return bits;
    }

    public static List<int> EncodeU32(uint value)
    {
        var bits = new List<int>();
        if (value == 0)
        {
            bits.Add(0);
            return bits;
        }
        bits.Add(1);
        int lzc = 0;
        for (int b = 31; b >= 0; b--)
        {
            if ((value & (1u << b)) != 0) break;
            lzc++;
        }
        for (int i = 0; i < 5; i++)
            bits.Add((lzc >> i) & 1);
        int vb = 32 - lzc;
        for (int i = 0; i < vb; i++)
            bits.Add((int)((value >> i) & 1));
        return bits;
    }
}
