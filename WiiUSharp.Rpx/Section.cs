namespace WiiUSharp.Rpx;

/// <summary>
/// One ELF section with its data held plain, whatever the file stored.
/// </summary>
public sealed class Section
{
    private byte[] _data;

    internal Section(byte[] header, byte[] data, bool storedCompressed, uint storedOffset)
    {
        NameOffset = BigEndian.ReadUInt32(header, 0x00);
        Type = (SectionType)BigEndian.ReadUInt32(header, 0x04);
        Flags = BigEndian.ReadUInt32(header, 0x08) & ~RpxFormat.ZlibFlag;
        Address = BigEndian.ReadUInt32(header, 0x0C);
        Link = BigEndian.ReadUInt32(header, 0x18);
        Info = BigEndian.ReadUInt32(header, 0x1C);
        AddressAlign = BigEndian.ReadUInt32(header, 0x20);
        EntrySize = BigEndian.ReadUInt32(header, 0x24);
        _data = data;
        StoredCompressed = storedCompressed;
        StoredOffset = storedOffset;
        StoredSize = BigEndian.ReadUInt32(header, 0x14);
    }

    /// <summary>
    /// Load address.
    /// </summary>
    public uint Address { get; }
    /// <summary>
    /// Required alignment.
    /// </summary>
    public uint AddressAlign { get; }
    /// <summary>
    /// Plain section bytes; assign to replace them.
    /// </summary>
    public byte[] Data
    {
        get => _data;
        set => _data = value ?? throw new ArgumentNullException(nameof(value));
    }
    /// <summary>
    /// Fixed entry size, or 0.
    /// </summary>
    public uint EntrySize { get; }
    /// <summary>
    /// Section flags without the zlib bit.
    /// </summary>
    public uint Flags { get; }
    /// <summary>
    /// True when the file holds bytes for this section.
    /// </summary>
    public bool HasData => StoredOffset != 0;
    /// <summary>
    /// sh_info.
    /// </summary>
    public uint Info { get; }
    /// <summary>
    /// sh_link.
    /// </summary>
    public uint Link { get; }
    /// <summary>
    /// Offset of the name in the section-name string table.
    /// </summary>
    public uint NameOffset { get; }
    /// <summary>
    /// True when the file held this section zlib-compressed.
    /// </summary>
    public bool StoredCompressed { get; }
    /// <summary>
    /// File offset the section was read from; 0 when it has no data.
    /// </summary>
    public uint StoredOffset { get; }
    /// <summary>
    /// Size field as read: the compressed size for compressed sections.
    /// </summary>
    public uint StoredSize { get; }
    /// <summary>
    /// Section type.
    /// </summary>
    public SectionType Type { get; }

    /// <summary>
    /// Section header bytes for the given placement.
    /// </summary>
    /// <param name="offset">File offset of the data.</param>
    /// <param name="size">Size field to write.</param>
    /// <param name="compressed">Set the zlib flag.</param>
    public byte[] HeaderBytes(uint offset, uint size, bool compressed)
    {
        var bytes = new byte[RpxFormat.SectionHeaderSize];
        BigEndian.Write(bytes, 0x00, NameOffset);
        BigEndian.Write(bytes, 0x04, (uint)Type);
        BigEndian.Write(bytes, 0x08, Flags | (compressed ? RpxFormat.ZlibFlag : 0));
        BigEndian.Write(bytes, 0x0C, Address);
        BigEndian.Write(bytes, 0x10, offset);
        BigEndian.Write(bytes, 0x14, size);
        BigEndian.Write(bytes, 0x18, Link);
        BigEndian.Write(bytes, 0x1C, Info);
        BigEndian.Write(bytes, 0x20, AddressAlign);
        BigEndian.Write(bytes, 0x24, EntrySize);
        return bytes;
    }
}
