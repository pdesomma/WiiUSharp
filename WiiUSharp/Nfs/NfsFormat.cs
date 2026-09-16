namespace WiiUSharp.Nfs;

/// <summary>
/// Fixed numbers of the NFS container.
/// </summary>
public static class NfsFormat
{
    /// <summary>
    /// Bytes per split file, header included.
    /// </summary>
    public const long FileSize = 0xFA00000;
    /// <summary>
    /// Packed sector at which the counter IV starts.
    /// </summary>
    public const int FirstCounterSector = 3;
    /// <summary>
    /// Header length in the first file.
    /// </summary>
    public const int HeaderSize = 0x200;
    /// <summary>
    /// Counter IV value for <see cref="FirstCounterSector"/>.
    /// </summary>
    public const uint InitialCounter = 0x1F00;
    /// <summary>
    /// Key length in bytes.
    /// </summary>
    public const int KeySize = 16;
    /// <summary>
    /// Most parts a header can hold.
    /// </summary>
    public const int MaxParts = (TrailerOffset - PartTableOffset) / PartEntrySize;
    /// <summary>
    /// Bytes per part entry.
    /// </summary>
    public const int PartEntrySize = 8;
    /// <summary>
    /// Header offset of the part table.
    /// </summary>
    public const int PartTableOffset = 0x14;
    /// <summary>
    /// Bytes per sector.
    /// </summary>
    public const int SectorSize = 0x8000;
    /// <summary>
    /// Header offset of the trailing magic.
    /// </summary>
    public const int TrailerOffset = 0x1FC;

    /// <summary>
    /// Name of the n-th split file.
    /// </summary>
    public static string FileName(int index) => $"hif_{index:D6}.nfs";
}
