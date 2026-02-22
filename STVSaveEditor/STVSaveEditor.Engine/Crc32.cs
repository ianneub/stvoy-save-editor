namespace STVSaveEditor.Engine;

public static class Crc32
{
    private const uint CustomInit = 0x61635263;
    private const uint Polynomial = 0xEDB88320;

    public static readonly uint[] Table = new uint[256];

    static Crc32()
    {
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int j = 0; j < 8; j++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ Polynomial : crc >> 1;
            }
            Table[i] = crc;
        }
    }

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        uint crc = CustomInit;
        foreach (byte b in data)
        {
            crc = (crc >> 8) ^ Table[(crc ^ b) & 0xFF];
        }
        return crc;
    }
}
