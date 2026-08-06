using System;
using System.IO;
using GameRes;

namespace GARbro.GUI.Linux;

public sealed class ArchiveEntryItem
{
    private ArchiveEntryItem()
    {
    }

    public Entry Entry { get; private set; }
    public string FullPath { get; private set; }
    public string Name { get; private set; }
    public string Type { get; private set; }
    public long Size { get; private set; }
    public long Offset { get; private set; }
    public bool IsArchiveEntry { get; private set; }
    public bool IsDirectory { get; private set; }
    public bool IsParentDirectory { get; private set; }
    public bool IsFileSystemItem { get { return !string.IsNullOrEmpty(FullPath); } }
    public string SizeText { get { return IsDirectory ? "" : FormatSize(Size); } }
    public string OffsetText { get { return Offset < 0 ? "" : $"0x{Offset:X8}"; } }

    public static ArchiveEntryItem FromEntry(Entry entry)
    {
        return new ArchiveEntryItem
        {
            Entry = entry,
            Name = entry.Name,
            Type = string.IsNullOrWhiteSpace(entry.Type) ? "file" : entry.Type,
            Size = entry.Size,
            Offset = entry.Offset,
            IsArchiveEntry = true
        };
    }

    public static ArchiveEntryItem FromDirectory(DirectoryInfo directory, bool isParent = false)
    {
        return new ArchiveEntryItem
        {
            FullPath = directory.FullName,
            Name = isParent ? ".." : directory.Name,
            Type = "folder",
            Size = 0,
            Offset = -1,
            IsDirectory = true,
            IsParentDirectory = isParent
        };
    }

    public static ArchiveEntryItem FromFile(FileInfo file)
    {
        var type = FormatCatalog.Instance.GetTypeFromName(file.FullName);
        var fallbackType = string.IsNullOrWhiteSpace(file.Extension)
            ? "file"
            : file.Extension.TrimStart('.').ToLowerInvariant();
        return new ArchiveEntryItem
        {
            FullPath = file.FullName,
            Name = file.Name,
            Type = string.IsNullOrWhiteSpace(type) ? fallbackType : type,
            Size = file.Length,
            Offset = -1
        };
    }

    private static string FormatSize(long size)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double value = Math.Max(0, size);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            ++unit;
        }
        return unit == 0 ? $"{size} B" : $"{value:0.##} {units[unit]}";
    }
}
