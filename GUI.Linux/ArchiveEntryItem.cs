using GameRes;

namespace GARbro.GUI.Linux;

public sealed class ArchiveEntryItem
{
    public ArchiveEntryItem(Entry entry)
    {
        Entry = entry;
        Name = entry.Name;
        Type = string.IsNullOrWhiteSpace(entry.Type) ? "file" : entry.Type;
        Size = entry.Size;
        Offset = entry.Offset;
    }

    public Entry Entry { get; }
    public string Name { get; }
    public string Type { get; }
    public uint Size { get; }
    public long Offset { get; }
    public string SizeText => FormatSize(Size);
    public string OffsetText => Offset < 0 ? "" : $"0x{Offset:X8}";

    private static string FormatSize(uint size)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double value = size;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            ++unit;
        }
        return unit == 0 ? $"{size} B" : $"{value:0.##} {units[unit]}";
    }
}
