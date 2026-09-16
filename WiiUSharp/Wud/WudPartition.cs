using WiiUSharp.Nus;

namespace WiiUSharp.Wud;

/// <summary>
/// One partition of a disc: its table entry plus the plain header at its start, which says where the FST is and carries the H3 tables of every hashed content.
/// </summary>
public sealed class WudPartition
{
    private readonly byte[] _header;

    /// <summary>
    /// Creates a new instance of the <see cref="WudPartition"/> class.
    /// </summary>
    /// <param name="index">Position in the partition table.</param>
    /// <param name="name">Entry name, e.g. SI, UP or GM followed by the title ID.</param>
    /// <param name="offset">Where the partition starts on the disc, in bytes.</param>
    /// <param name="header">The whole partition header, plain.</param>
    public WudPartition(int index, string name, long offset, byte[] header)
    {
        if (name is null)
            throw new ArgumentNullException(nameof(name));
        if (header is null)
            throw new ArgumentNullException(nameof(header));
        if (header.Length < WudFormat.PartitionHeaderFixedSize)
            throw new ArgumentException($"A partition header is at least {WudFormat.PartitionHeaderFixedSize} bytes.", nameof(header));
        if (BigEndian.ReadUInt32(header, 0) != WudFormat.PartitionMagic)
            throw new InvalidDataException($"Partition {name} has no header magic.");

        Index = index;
        Name = name;
        Offset = offset;
        _header = (byte[])header.Clone();
    }

    /// <summary>
    /// Where the FST and contents are measured from: the partition start plus its header.
    /// </summary>
    public long DataOffset => Offset + HeaderSize;
    /// <summary>
    /// 1 when the FST is encrypted with the disc key, 2 with the partition's title key.
    /// </summary>
    public byte FstEncryptionType => _header[0x25];
    /// <summary>
    /// 0 when the FST is stored plain, 1 when its blocks are hashed.
    /// </summary>
    public byte FstHashType => _header[0x24];
    /// <summary>
    /// Where the FST starts on the disc, in bytes.
    /// </summary>
    public long FstOffset => Offset + (long)BigEndian.ReadUInt32(_header, 0x18) * WudFormat.SectorSize;
    /// <summary>
    /// FST length in bytes.
    /// </summary>
    public int FstSize => checked((int)BigEndian.ReadUInt32(_header, 0x14));
    /// <summary>
    /// Number of words ahead of the H3 tables.
    /// </summary>
    public int H3Count => checked((int)BigEndian.ReadUInt32(_header, 0x10));
    /// <summary>
    /// Header length in bytes; the FST and contents follow it.
    /// </summary>
    public long HeaderSize => BigEndian.ReadUInt32(_header, 0x04);
    /// <summary>
    /// Position in the partition table.
    /// </summary>
    public int Index { get; }
    /// <summary>
    /// True for a partition that holds a game title.
    /// </summary>
    public bool IsGame => Name.StartsWith(WudFormat.GamePartitionPrefix, StringComparison.Ordinal);
    /// <summary>
    /// Entry name.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Where the partition starts on the disc, in bytes.
    /// </summary>
    public long Offset { get; }

    /// <summary>
    /// The H3 tables, one after another in content order; the header stores them after its word table.
    /// </summary>
    /// <param name="offset">Bytes into the tables.</param>
    /// <param name="count">Bytes to take.</param>
    /// <exception cref="InvalidDataException">The header does not hold that many.</exception>
    public byte[] H3Tables(int offset, int count)
    {
        var start = WudFormat.PartitionHeaderFixedSize + H3Count * 4 + offset;
        if (offset < 0 || count < 0 || start + count > _header.Length)
            throw new InvalidDataException($"Partition {Name} header holds no H3 table at {offset}+{count}.");

        var bytes = new byte[count];
        Array.Copy(_header, start, bytes, 0, count);
        return bytes;
    }

    /// <summary>
    /// Copy of the raw header.
    /// </summary>
    public byte[] ToBytes() => (byte[])_header.Clone();
}
