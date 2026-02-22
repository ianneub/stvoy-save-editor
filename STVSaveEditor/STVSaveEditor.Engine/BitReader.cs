using System.Text;

namespace STVSaveEditor.Engine;

public class BitReader
{
    private readonly byte[] _data;
    public int Pos { get; set; }

    public BitReader(byte[] data, int pos = 0)
    {
        _data = data;
        Pos = pos;
    }

    public int ReadBool()
    {
        int v = (_data[Pos >> 3] >> (Pos & 7)) & 1;
        Pos++;
        return v;
    }

    public int ReadBits(int count)
    {
        int v = 0;
        for (int i = 0; i < count; i++)
            v |= ReadBool() << i;
        return v;
    }

    public uint ReadBitsU(int count)
    {
        uint v = 0;
        for (int i = 0; i < count; i++)
            v |= (uint)ReadBool() << i;
        return v;
    }

    public void DebugSkip(int oid)
    {
        ReadBool();
        if (oid != -1)
            ReadBits(32);
    }

    public uint ReadU32Packed(int oid = -1)
    {
        DebugSkip(oid);
        if (ReadBool() == 0) return 0;
        int lzc = ReadBits(5);
        int vb = 32 - lzc;
        return vb > 0 ? ReadBitsU(vb) : 0;
    }

    public int ReadI32Packed(int oid = -1)
    {
        DebugSkip(oid);
        if (ReadBool() == 0) return 0;
        int sign = ReadBool();
        int lzc = ReadBits(5);
        int vb = 32 - lzc;
        int v = vb > 0 ? ReadBits(vb) : 0;
        return sign != 0 ? -v : v;
    }

    public long ReadU64Packed(int oid = -1)
    {
        DebugSkip(oid);
        uint lo = ReadU32Packed(-1);
        uint hi = ReadU32Packed(-1);
        return ((long)hi << 32) | lo;
    }

    public int ReadBoolWrapped(int oid = -1)
    {
        DebugSkip(oid);
        return ReadBool();
    }

    public string ReadString(int oid = -1)
    {
        DebugSkip(oid);
        uint length = ReadU32Packed(-1);
        if (length == 0) return "";
        int charBitWidth = ReadBits(4);
        int baseChar = ReadBits(8);
        var sb = new StringBuilder((int)length);
        for (uint i = 0; i < length; i++)
            sb.Append((char)(ReadBits(charBitWidth) + baseChar));
        return sb.ToString();
    }

    public float ReadFloat(int oid = -1)
    {
        DebugSkip(oid);
        uint raw = ReadU32Packed(-1);
        return BitConverter.Int32BitsToSingle((int)raw);
    }

    public ChunkHeader ReadChunkHeader()
    {
        var ch = new ChunkHeader();
        if (ReadBool() != 0)
            ch.Parent = Encoding.ASCII.GetString(BitConverter.GetBytes(ReadBitsU(32)));
        ch.Tag = Encoding.ASCII.GetString(BitConverter.GetBytes(ReadU32Packed(-1)));
        ch.Version = ReadU32Packed(-1);
        ch.SubVersion = ReadU32Packed(-1);
        ch.SizePos = Pos;
        ch.Size = ReadBits(32);
        ch.DataStart = Pos;
        ch.DataEnd = Pos + ch.Size;
        return ch;
    }

    public void SkipChunk(ChunkHeader ch)
    {
        Pos = ch.DataEnd;
        if (ReadBool() != 0)
            ReadBits(32);
    }

    public SubChunkHeader ReadChunkStart(int oid = -1)
    {
        DebugSkip(oid);
        var sc = new SubChunkHeader
        {
            Version = ReadU32Packed(-1),
            SubVersion = ReadU32Packed(-1),
            Val3 = ReadU32Packed(-1),
        };
        sc.Size = ReadBits(32);
        sc.DataStart = Pos;
        sc.DataEnd = Pos + sc.Size;
        return sc;
    }
}

public class ChunkHeader
{
    public string? Parent { get; set; }
    public string Tag { get; set; } = "";
    public uint Version { get; set; }
    public uint SubVersion { get; set; }
    public int SizePos { get; set; }
    public int Size { get; set; }
    public int DataStart { get; set; }
    public int DataEnd { get; set; }
}

public class SubChunkHeader
{
    public uint Version { get; set; }
    public uint SubVersion { get; set; }
    public uint Val3 { get; set; }
    public int Size { get; set; }
    public int DataStart { get; set; }
    public int DataEnd { get; set; }
}
