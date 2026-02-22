namespace STVSaveEditor.Engine;

public class ResourceEntry
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
    public int QuantityBitPos { get; set; }
    public bool IsItem { get; set; }
}
