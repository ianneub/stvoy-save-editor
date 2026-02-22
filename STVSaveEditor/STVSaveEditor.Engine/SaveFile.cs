namespace STVSaveEditor.Engine;

public static class SaveFile
{
    public static byte[] Load(string path)
    {
        byte[] raw = File.ReadAllBytes(path);
        byte[] data = Convert.FromBase64String(System.Text.Encoding.ASCII.GetString(raw));
        uint fileSize = BitConverter.ToUInt32(data, 0);
        if (fileSize != data.Length)
            throw new InvalidDataException($"File size mismatch: header={fileSize}, actual={data.Length}");
        return data;
    }

    public static void Save(byte[] data, string path)
    {
        uint newHash = Crc32.Compute(data.AsSpan(16));
        BitConverter.GetBytes(newHash).CopyTo(data, 4);
        string encoded = Convert.ToBase64String(data);
        File.WriteAllText(path, encoded, System.Text.Encoding.ASCII);
    }

    public static bool MakeBackup(string path)
    {
        string backup = path + ".backup";
        if (File.Exists(backup))
            return false;
        File.Copy(path, backup);
        return true;
    }
}
