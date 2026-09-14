namespace WiiUSharp.Rpx;

/// <summary>
/// Constants of the RPX/RPL executable format.
/// </summary>
public static class RpxFormat
{
    /// <summary>
    /// Bytes each section's data is aligned to when written.
    /// </summary>
    public const int DataAlignment = 0x40;
    /// <summary>
    /// ELF magic, big-endian.
    /// </summary>
    public const uint ElfMagic = 0x7F454C46;
    /// <summary>
    /// ELF header length.
    /// </summary>
    public const int HeaderSize = 0x34;
    /// <summary>
    /// e_type of every RPX and RPL.
    /// </summary>
    public const ushort RplType = 0xFE01;
    /// <summary>
    /// Section header length.
    /// </summary>
    public const int SectionHeaderSize = 0x28;
    /// <summary>
    /// Section flag: data is a big-endian length followed by a zlib stream.
    /// </summary>
    public const uint ZlibFlag = 0x08000000;
    /// <summary>
    /// Sections smaller than this are only compressed when it saves space.
    /// </summary>
    public const int ZlibThreshold = 16384;
}
