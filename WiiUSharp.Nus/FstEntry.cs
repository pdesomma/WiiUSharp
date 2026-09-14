namespace WiiUSharp.Nus;

/// <summary>
/// A file or folder in the title, with the values its FST entry will carry.
/// </summary>
internal sealed class FstEntry
{
    public const byte DirectoryType = 0x01;
    public const int Size = 0x10;
    public const byte WiiVcType = 0x02;

    public FstEntry(string name, string? path, bool isDirectory, long length, FstEntry? parent)
    {
        Name = name;
        Path = path;
        IsDirectory = isDirectory;
        Length = length;
        Parent = parent;
    }

    public List<FstEntry> Children { get; } = new();
    public ContentPlan? Content { get; set; }
    public int Index { get; set; }
    public bool IsDirectory { get; }
    public bool IsRoot => Parent is null;
    public long Length { get; }
    public string Name { get; }
    public int NameOffset { get; set; }
    public long Offset { get; set; }
    public FstEntry? Parent { get; }
    /// <summary>
    /// On-disk path; null for the root.
    /// </summary>
    public string? Path { get; }
    public int SubtreeCount => 1 + Children.Sum(c => c.SubtreeCount);
    public string TitlePath => IsRoot ? "" : Parent!.TitlePath + "/" + Name;
    public byte Type => (byte)((IsDirectory ? DirectoryType : 0) | (Name.EndsWith("nfs", StringComparison.Ordinal) ? WiiVcType : 0));

    /// <summary>
    /// This entry then every descendant, files before folders.
    /// </summary>
    public IEnumerable<FstEntry> Traverse()
    {
        yield return this;
        foreach (var child in Children)
            foreach (var entry in child.Traverse())
                yield return entry;
    }

    public byte[] ToBytes()
    {
        var bytes = new byte[Size];
        if (IsRoot)
        {
            bytes[0] = DirectoryType;
            BigEndian.Write(bytes, 0x08, (uint)SubtreeCount);
            return bytes;
        }

        bytes[0] = Type;
        BigEndian.Write24(bytes, 0x01, NameOffset);
        if (IsDirectory)
        {
            BigEndian.Write(bytes, 0x04, (uint)Parent!.Index);
            BigEndian.Write(bytes, 0x08, (uint)(Index + SubtreeCount));
        }
        else
        {
            BigEndian.Write(bytes, 0x04, (uint)(Offset >> 5));
            BigEndian.Write(bytes, 0x08, (uint)Length);
        }
        BigEndian.Write(bytes, 0x0C, Content!.Details.EntryFlags);
        BigEndian.Write(bytes, 0x0E, (ushort)Content.Index);
        return bytes;
    }
}
