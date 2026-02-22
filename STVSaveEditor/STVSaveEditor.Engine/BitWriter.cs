namespace STVSaveEditor.Engine;

public static class BitWriter
{
    public static int GetBit(byte[] data, int pos)
    {
        return (data[pos >> 3] >> (pos & 7)) & 1;
    }

    public static int ReadBitsAt(byte[] data, int pos, int count)
    {
        int v = 0;
        for (int i = 0; i < count; i++)
            v |= GetBit(data, pos + i) << i;
        return v;
    }

    public static void SetBit(byte[] data, int pos, int val)
    {
        int byteIdx = pos >> 3;
        int bitIdx = pos & 7;
        if (val != 0)
            data[byteIdx] |= (byte)(1 << bitIdx);
        else
            data[byteIdx] &= (byte)~(1 << bitIdx);
    }

    public static void WriteBits(byte[] data, int pos, int value, int count)
    {
        for (int i = 0; i < count; i++)
            SetBit(data, pos + i, (value >> i) & 1);
    }

    public static void WriteBits(byte[] data, int pos, uint value, int count)
    {
        for (int i = 0; i < count; i++)
            SetBit(data, pos + i, (int)((value >> i) & 1));
    }

    public static void CopyBits(byte[] src, int srcPos, byte[] dst, int dstPos, int count)
    {
        for (int i = 0; i < count; i++)
            SetBit(dst, dstPos + i, GetBit(src, srcPos + i));
    }
}
