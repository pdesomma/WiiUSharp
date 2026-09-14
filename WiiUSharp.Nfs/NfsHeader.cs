namespace WiiUSharp.Nfs;

/// <summary>
/// The 0x200-byte header at the start of hif_000000.nfs.
/// </summary>
public sealed class NfsHeader
{
    private static readonly byte[] Magic = { 0x45, 0x47, 0x47, 0x53 };
    private static readonly byte[] Trailer = { 0x53, 0x47, 0x47, 0x45 };
    private static readonly byte[] Version = { 0x00, 0x01, 0x10, 0x11 };

    /// <summary>
    /// Creates a new instance of the <see cref="NfsHeader"/> class.
    /// </summary>
    /// <param name="parts">Stored runs, in payload order.</param>
    /// <exception cref="ArgumentException">Too many parts, or parts overlap or are unordered.</exception>
    public NfsHeader(IReadOnlyList<NfsPart> parts)
    {
        if (parts is null)
            throw new ArgumentNullException(nameof(parts));
        if (parts.Count > NfsFormat.MaxParts)
            throw new ArgumentException($"At most {NfsFormat.MaxParts} parts fit in the header.", nameof(parts));

        for (var i = 1; i < parts.Count; i++)
            if (parts[i].StartOffset < parts[i - 1].EndOffset)
                throw new ArgumentException("Parts must be ascending and must not overlap.", nameof(parts));

        Parts = parts.ToArray();
    }

    /// <summary>
    /// Payload length implied by the last part.
    /// </summary>
    public long PayloadLength => Parts.Count == 0 ? 0 : Parts[Parts.Count - 1].EndOffset;
    /// <summary>
    /// Bytes stored across all parts.
    /// </summary>
    public long PackedLength => Parts.Sum(p => p.Length);
    /// <summary>
    /// Stored runs, in payload order.
    /// </summary>
    public IReadOnlyList<NfsPart> Parts { get; }

    /// <summary>
    /// The three-part layout nfs2iso2nfs writes: disc header, partition tables, game data.
    /// </summary>
    /// <param name="data">Game partition range; offset must be sector aligned.</param>
    /// <exception cref="ArgumentException">Offset not sector aligned or range too small.</exception>
    public static NfsHeader ForDisc(DiscDataSpan data)
    {
        if (data.Offset % NfsFormat.SectorSize != 0)
            throw new ArgumentException("Offset must be a multiple of the sector size.", nameof(data));
        if (data.Offset < 10L * NfsFormat.SectorSize || data.Length <= 0)
            throw new ArgumentException("Game data must start after the partition tables and be non-empty.", nameof(data));

        var sectors = (data.Length + NfsFormat.SectorSize - 1) / NfsFormat.SectorSize;
        return new NfsHeader(new[]
        {
            new NfsPart(0, 1),
            new NfsPart(8, 2),
            new NfsPart(checked((uint)(data.Offset / NfsFormat.SectorSize)), checked((uint)sectors)),
        });
    }

    /// <summary>
    /// Parses the header bytes.
    /// </summary>
    /// <param name="bytes">At least 0x200 bytes.</param>
    /// <exception cref="FormatException">Not an NFS header.</exception>
    public static NfsHeader Parse(byte[] bytes)
    {
        if (!TryParse(bytes, out var header))
            throw new FormatException("Not an NFS header.");
        return header!;
    }

    /// <summary>
    /// Header bytes as written to the first file.
    /// </summary>
    public byte[] ToBytes()
    {
        var bytes = new byte[NfsFormat.HeaderSize];
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = 0xFF;

        Magic.CopyTo(bytes, 0);
        Version.CopyTo(bytes, 4);
        Array.Clear(bytes, 8, 8);
        WriteUInt32(bytes, 0x10, (uint)Parts.Count);
        for (var i = 0; i < Parts.Count; i++)
        {
            var at = NfsFormat.PartTableOffset + i * NfsFormat.PartEntrySize;
            WriteUInt32(bytes, at, Parts[i].StartSector);
            WriteUInt32(bytes, at + 4, Parts[i].SectorCount);
        }
        Trailer.CopyTo(bytes, NfsFormat.TrailerOffset);
        return bytes;
    }

    /// <summary>
    /// Parses the header bytes without throwing.
    /// </summary>
    /// <param name="bytes">At least 0x200 bytes.</param>
    /// <param name="header">Parsed header, or null on failure.</param>
    public static bool TryParse(byte[]? bytes, out NfsHeader? header)
    {
        header = null;
        if (bytes is null || bytes.Length < NfsFormat.HeaderSize)
            return false;
        if (!Matches(bytes, 0, Magic) || !Matches(bytes, NfsFormat.TrailerOffset, Trailer))
            return false;

        var count = ReadUInt32(bytes, 0x10);
        if (count > NfsFormat.MaxParts)
            return false;

        var parts = new NfsPart[count];
        for (var i = 0; i < count; i++)
        {
            var at = NfsFormat.PartTableOffset + i * NfsFormat.PartEntrySize;
            parts[i] = new NfsPart(ReadUInt32(bytes, at), ReadUInt32(bytes, at + 4));
            if (i > 0 && parts[i].StartOffset < parts[i - 1].EndOffset)
                return false;
        }

        header = new NfsHeader(parts);
        return true;
    }

    private static bool Matches(byte[] bytes, int offset, byte[] expected)
    {
        for (var i = 0; i < expected.Length; i++)
            if (bytes[offset + i] != expected[i])
                return false;
        return true;
    }

    private static uint ReadUInt32(byte[] bytes, int offset) =>
        (uint)(bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3]);

    private static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)(value >> 24);
        bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8);
        bytes[offset + 3] = (byte)value;
    }
}
