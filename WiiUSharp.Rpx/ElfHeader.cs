namespace WiiUSharp.Rpx;

/// <summary>
/// The 0x34-byte ELF header of an RPX or RPL.
/// </summary>
public sealed class ElfHeader
{
    private readonly byte[] _bytes;

    private ElfHeader(byte[] bytes)
    {
        _bytes = bytes;
    }

    /// <summary>
    /// Entry point address.
    /// </summary>
    public uint Entry => BigEndian.ReadUInt32(_bytes, 0x18);
    /// <summary>
    /// Processor flags.
    /// </summary>
    public uint Flags => BigEndian.ReadUInt32(_bytes, 0x24);
    /// <summary>
    /// The e_ident bytes.
    /// </summary>
    public byte[] Identification => _bytes.Take(0x10).ToArray();
    /// <summary>
    /// Target machine; 0x14 is PowerPC.
    /// </summary>
    public ushort Machine => BigEndian.ReadUInt16(_bytes, 0x12);
    /// <summary>
    /// Section header count.
    /// </summary>
    public ushort SectionCount => BigEndian.ReadUInt16(_bytes, 0x30);
    /// <summary>
    /// Bytes per section header.
    /// </summary>
    public ushort SectionHeaderSize => BigEndian.ReadUInt16(_bytes, 0x2E);
    /// <summary>
    /// File offset of the section header table.
    /// </summary>
    public uint SectionHeadersOffset => BigEndian.ReadUInt32(_bytes, 0x20);
    /// <summary>
    /// Index of the section-name string table.
    /// </summary>
    public ushort SectionNamesIndex => BigEndian.ReadUInt16(_bytes, 0x32);
    /// <summary>
    /// File type; <see cref="RpxFormat.RplType"/> for RPX and RPL.
    /// </summary>
    public ushort Type => BigEndian.ReadUInt16(_bytes, 0x10);

    /// <summary>
    /// Parses a header and checks the ELF magic and RPL type.
    /// </summary>
    /// <param name="bytes">At least <see cref="RpxFormat.HeaderSize"/> bytes.</param>
    /// <exception cref="InvalidDataException">Not an RPX or RPL.</exception>
    public static ElfHeader Parse(byte[] bytes)
    {
        if (bytes is null)
            throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length < RpxFormat.HeaderSize)
            throw new ArgumentException($"Need at least {RpxFormat.HeaderSize} bytes.", nameof(bytes));
        if (BigEndian.ReadUInt32(bytes, 0) != RpxFormat.ElfMagic)
            throw new InvalidDataException("Not an ELF file.");

        var header = new ElfHeader(bytes.Take(RpxFormat.HeaderSize).ToArray());
        if (header.Type != RpxFormat.RplType)
            throw new InvalidDataException($"ELF type {header.Type:X4} is not an RPX or RPL.");
        if (header.SectionHeaderSize != RpxFormat.SectionHeaderSize)
            throw new InvalidDataException($"Section header size {header.SectionHeaderSize} is not {RpxFormat.SectionHeaderSize}.");
        return header;
    }

    /// <summary>
    /// The header bytes.
    /// </summary>
    public byte[] ToBytes() => (byte[])_bytes.Clone();
}
