using System.Text;

namespace WiiUSharp.Nus;

/// <summary>
/// Reads the file system table of content 0: which file sits where in which content.
/// </summary>
public sealed class Fst
{
    /// <summary>
    /// Entry flag meaning the offset is stored unshifted.
    /// </summary>
    public const ushort UnshiftedOffsetFlag = 0x0004;

    private const int ContentHeaderSize = 0x20;
    private const int EntrySize = 0x10;
    private const int HeaderSize = 0x20;
    private static readonly byte[] Magic = { 0x46, 0x53, 0x54, 0x00 };

    private Fst(int contentCount, IReadOnlyList<FstFile> files)
    {
        ContentCount = contentCount;
        Files = files;
    }

    /// <summary>
    /// Contents the table describes, the FST itself included.
    /// </summary>
    public int ContentCount { get; }
    /// <summary>
    /// Every file, in table order.
    /// </summary>
    public IReadOnlyList<FstFile> Files { get; }

    /// <summary>
    /// True when the bytes start with the FST magic.
    /// </summary>
    /// <param name="bytes">Decrypted start of content 0.</param>
    public static bool HasMagic(byte[] bytes)
    {
        if (bytes is null)
            throw new ArgumentNullException(nameof(bytes));

        return bytes.Length >= Magic.Length && bytes.Take(Magic.Length).SequenceEqual(Magic);
    }

    /// <summary>
    /// Parses a decrypted FST.
    /// </summary>
    /// <param name="bytes">Decrypted content 0.</param>
    /// <exception cref="InvalidDataException">Wrong magic or a table that runs past the end.</exception>
    public static Fst Parse(byte[] bytes)
    {
        if (!HasMagic(bytes))
            throw new InvalidDataException("Not an FST: magic is missing.");
        if (bytes.Length < HeaderSize)
            throw new InvalidDataException("FST is shorter than its header.");

        var contentCount = BigEndian.ReadUInt32(bytes, 0x08);
        if (HeaderSize + (long)contentCount * ContentHeaderSize + EntrySize > bytes.Length)
            throw new InvalidDataException("FST declares more contents than it holds.");

        var entriesAt = HeaderSize + (int)contentCount * ContentHeaderSize;
        var entryCount = BigEndian.ReadUInt32(bytes, entriesAt + 0x08);
        if (entryCount < 1 || entriesAt + (long)entryCount * EntrySize > bytes.Length)
            throw new InvalidDataException("FST declares more entries than it holds.");
        var stringsAt = entriesAt + (int)entryCount * EntrySize;

        var files = new List<FstFile>();
        var folders = new Stack<(int End, string Path)>();
        folders.Push(((int)entryCount, ""));
        for (var i = 1; i < entryCount; i++)
        {
            while (i >= folders.Peek().End)
                folders.Pop();

            var at = entriesAt + i * EntrySize;
            var name = Name(bytes, stringsAt + BigEndian.Read24(bytes, at + 0x01));
            var path = folders.Peek().Path + "/" + name;
            if ((bytes[at] & FstEntry.DirectoryType) != 0)
            {
                folders.Push(((int)BigEndian.ReadUInt32(bytes, at + 0x08), path));
                continue;
            }

            var flags = BigEndian.ReadUInt16(bytes, at + 0x0C);
            long offset = BigEndian.ReadUInt32(bytes, at + 0x04);
            if ((flags & UnshiftedOffsetFlag) == 0)
                offset <<= 5;
            files.Add(new FstFile(path, BigEndian.ReadUInt16(bytes, at + 0x0E), offset, BigEndian.ReadUInt32(bytes, at + 0x08), flags));
        }
        return new Fst((int)contentCount, files);
    }

    private static string Name(byte[] bytes, int offset)
    {
        if (offset >= bytes.Length)
            throw new InvalidDataException("FST name offset runs past the end.");

        var end = Array.IndexOf(bytes, (byte)0, offset);
        if (end < 0)
            throw new InvalidDataException("FST name is not terminated.");
        return Encoding.UTF8.GetString(bytes, offset, end - offset);
    }
}
