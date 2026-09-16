namespace WiiUSharp.Wud;

/// <summary>
/// Fixed offsets and magics of a Wii U disc image.
/// </summary>
public static class WudFormat
{
    /// <summary>
    /// Every disc dumps to this many bytes.
    /// </summary>
    public const long DiscSize = 0x5D3A00000;
    /// <summary>
    /// Sector the disc structures are laid out in.
    /// </summary>
    public const int SectorSize = 0x8000;
    /// <summary>
    /// Where the plain disc magic sits.
    /// </summary>
    public const long DiscMagicOffset = 0x10000;
    /// <summary>
    /// Value at <see cref="DiscMagicOffset"/>.
    /// </summary>
    public const uint DiscMagic = 0xCC549EB9;
    /// <summary>
    /// Where the disc-key-encrypted partition table sits.
    /// </summary>
    public const long PartitionTableOffset = 0x18000;
    /// <summary>
    /// First word of the decrypted partition table.
    /// </summary>
    public const uint PartitionTableMagic = 0xCCA6E67B;
    /// <summary>
    /// Where the entries start inside the partition table sector.
    /// </summary>
    public const int PartitionEntriesOffset = 0x800;
    /// <summary>
    /// Bytes per partition table entry.
    /// </summary>
    public const int PartitionEntrySize = 0x80;
    /// <summary>
    /// Bytes of a partition entry's name.
    /// </summary>
    public const int PartitionNameSize = 0x19;
    /// <summary>
    /// First word of every partition header.
    /// </summary>
    public const uint PartitionMagic = 0xCC93A4F5;
    /// <summary>
    /// Bytes of the fixed part of a partition header, before the H3 tables.
    /// </summary>
    public const int PartitionHeaderFixedSize = 0x40;
    /// <summary>
    /// Bytes of the product code at the very start of the disc.
    /// </summary>
    public const int ProductCodeSize = 0x16;
    /// <summary>
    /// Name of the disc key file dumps ship with.
    /// </summary>
    public const string DiscKeyFileName = "game.key";
    /// <summary>
    /// Partitions whose name starts with this hold a game title.
    /// </summary>
    public const string GamePartitionPrefix = "GM";
    /// <summary>
    /// The partition that holds every game partition's ticket, TMD and certificate chain.
    /// </summary>
    public const string SystemInformationPartition = "SI";
}
