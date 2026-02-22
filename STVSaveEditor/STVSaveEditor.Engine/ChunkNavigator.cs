namespace STVSaveEditor.Engine;

public static class ChunkNavigator
{
    private const int TagRict = 0x74636972; // "rict"
    private const int TagRinm = 0x6d6e6972; // "rinm"
    private const int TagShct = 0x73686374; // "shct"

    public static (BitReader reader, ChunkHeader chunk) NavigateToCser(byte[] data)
    {
        var r = new BitReader(data, 128);
        var parw = r.ReadChunkHeader();
        var daeh = r.ReadChunkHeader();
        r.SkipChunk(daeh);
        var emag = r.ReadChunkHeader();
        var trts = r.ReadChunkHeader();
        var cser = r.ReadChunkHeader();
        return (r, cser);
    }

    public static (BitReader reader, ChunkHeader chunk) NavigateToTsnc(byte[] data)
    {
        var r = new BitReader(data, 128);
        var parw = r.ReadChunkHeader();
        var daeh = r.ReadChunkHeader();
        r.SkipChunk(daeh);
        var emag = r.ReadChunkHeader();
        var trts = r.ReadChunkHeader();
        var cser = r.ReadChunkHeader();
        r.SkipChunk(cser);
        var psjv = r.ReadChunkHeader();
        r.SkipChunk(psjv);
        var tces = r.ReadChunkHeader();
        r.SkipChunk(tces);
        var tsnc = r.ReadChunkHeader();
        return (r, tsnc);
    }

    public static List<int> GetChunkSizePositions(byte[] data)
    {
        var r = new BitReader(data, 128);
        var parw = r.ReadChunkHeader();
        var daeh = r.ReadChunkHeader();
        r.SkipChunk(daeh);
        var emag = r.ReadChunkHeader();
        var trts = r.ReadChunkHeader();
        var cser = r.ReadChunkHeader();
        return new List<int> { parw.SizePos, emag.SizePos, trts.SizePos, cser.SizePos };
    }

    public static List<ResourceEntry> ReadResources(byte[] data)
    {
        var (r, cser) = NavigateToCser(data);
        int count = r.ReadI32Packed(TagRict);
        var resources = new List<ResourceEntry>(count);
        for (int i = 0; i < count; i++)
        {
            int idx = (int)r.ReadU32Packed(-1);
            string name = r.ReadString(TagRinm);
            int qtyPos = r.Pos;
            int qty = r.ReadI32Packed(-1);
            int flag = r.ReadBoolWrapped(-1);
            resources.Add(new ResourceEntry
            {
                Index = idx,
                Name = name,
                Quantity = qty,
                QuantityBitPos = qtyPos,
                IsItem = flag != 0,
            });
        }
        return resources;
    }

    public static int? FindShctPosition(byte[] data, ChunkHeader tsnc)
    {
        for (int pos = tsnc.DataStart; pos < tsnc.DataEnd - 33; pos++)
        {
            if (BitWriter.GetBit(data, pos) == 1)
            {
                int val = BitWriter.ReadBitsAt(data, pos + 1, 32);
                if (val == TagShct)
                {
                    var r = new BitReader(data, pos);
                    r.ReadI32Packed(TagShct);
                    return r.Pos;
                }
            }
        }
        return null;
    }

    public static HullData ReadHullIntegrity(byte[] data)
    {
        var (r, tsnc) = NavigateToTsnc(data);
        int? afterShct = FindShctPosition(data, tsnc);
        if (afterShct == null)
            throw new InvalidDataException("Could not find shct tag in tsnc chunk");

        int hullPos = afterShct.Value + 8;
        int raw = BitWriter.ReadBitsAt(data, hullPos, 32);
        float hull = BitConverter.Int32BitsToSingle(raw);
        return new HullData { Value = hull, BitPos = hullPos };
    }

    public static void SetHullIntegrity(byte[] data, float newValue)
    {
        var hull = ReadHullIntegrity(data);
        int rawNew = BitConverter.SingleToInt32Bits(newValue);
        BitWriter.WriteBits(data, hull.BitPos, rawNew, 32);
    }

    public static byte[] ModifyResources(byte[] data, Dictionary<string, int> modifications)
    {
        var resources = ReadResources(data);
        var sizePositions = GetChunkSizePositions(data);

        var patches = new List<(int pos, int oldLen, List<int> newBits)>();
        foreach (var res in resources)
        {
            if (modifications.TryGetValue(res.Name, out int newVal))
            {
                var oldBits = PackedEncoding.EncodeI32(res.Quantity);
                var newBits = PackedEncoding.EncodeI32(newVal);
                patches.Add((res.QuantityBitPos + 1, oldBits.Count, newBits));
            }
        }

        if (patches.Count == 0) return data;

        patches.Sort((a, b) => a.pos.CompareTo(b.pos));

        int totalDelta = patches.Sum(p => p.newBits.Count - p.oldLen);
        int totalBits = data.Length * 8;
        int newTotalBits = totalBits + totalDelta;
        byte[] newData = new byte[(newTotalBits + 7) / 8];

        int srcPos = 0, dstPos = 0;
        foreach (var (pos, oldLen, newBits) in patches)
        {
            BitWriter.CopyBits(data, srcPos, newData, dstPos, pos - srcPos);
            dstPos += pos - srcPos;
            srcPos = pos;

            for (int i = 0; i < newBits.Count; i++)
                BitWriter.SetBit(newData, dstPos + i, newBits[i]);
            dstPos += newBits.Count;
            srcPos += oldLen;
        }

        BitWriter.CopyBits(data, srcPos, newData, dstPos, totalBits - srcPos);

        foreach (int sizePos in sizePositions)
        {
            int oldSize = BitWriter.ReadBitsAt(data, sizePos, 32);
            int newSize = oldSize + totalDelta;
            BitWriter.WriteBits(newData, sizePos, newSize, 32);
        }

        BitConverter.GetBytes((uint)newData.Length).CopyTo(newData, 0);

        return newData;
    }
}
