namespace WiiUSharp.Nfs;

/// <summary>
/// Run of payload sectors stored in the container.
/// </summary>
/// <param name="StartSector">First payload sector.</param>
/// <param name="SectorCount">Sectors stored.</param>
public readonly record struct NfsPart(uint StartSector, uint SectorCount)
{
    /// <summary>
    /// Payload offset one past the last stored byte.
    /// </summary>
    public long EndOffset => StartOffset + Length;
    /// <summary>
    /// Stored bytes.
    /// </summary>
    public long Length => (long)SectorCount * NfsFormat.SectorSize;
    /// <summary>
    /// Payload offset of the first stored byte.
    /// </summary>
    public long StartOffset => (long)StartSector * NfsFormat.SectorSize;
}
