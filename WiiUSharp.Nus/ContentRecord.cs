namespace WiiUSharp.Nus;

/// <summary>
/// One content as the TMD describes it.
/// </summary>
public sealed class ContentRecord
{
    /// <summary>
    /// Creates a new instance of the <see cref="ContentRecord"/> class.
    /// </summary>
    /// <param name="index">Position in the package; also the ID and file name.</param>
    /// <param name="type">Type bits.</param>
    /// <param name="size">Encrypted file length.</param>
    /// <param name="hash">SHA-1 the TMD carries.</param>
    public ContentRecord(int index, ContentType type, long size, byte[] hash)
    {
        if (hash is null)
            throw new ArgumentNullException(nameof(hash));
        if (hash.Length != NusFormat.HashSize)
            throw new ArgumentException($"Hash must be {NusFormat.HashSize} bytes.", nameof(hash));
        if (index < 0 || index > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (size < 0)
            throw new ArgumentOutOfRangeException(nameof(size));

        Index = index;
        Type = type;
        Size = size;
        Hash = (byte[])hash.Clone();
    }

    /// <summary>
    /// SHA-1 the TMD carries.
    /// </summary>
    public byte[] Hash { get; }
    /// <summary>
    /// Position in the package; also the ID and file name.
    /// </summary>
    public int Index { get; }
    /// <summary>
    /// True when the content uses the hashed layout.
    /// </summary>
    public bool IsHashed => (Type & ContentType.Hashed) != 0;
    /// <summary>
    /// Encrypted file length.
    /// </summary>
    public long Size { get; }
    /// <summary>
    /// Type bits.
    /// </summary>
    public ContentType Type { get; }

    /// <summary>
    /// The 0x30-byte TMD record.
    /// </summary>
    public byte[] ToBytes()
    {
        var bytes = new byte[NusFormat.ContentRecordSize];
        BigEndian.Write(bytes, 0x00, (uint)Index);
        BigEndian.Write(bytes, 0x04, (ushort)Index);
        BigEndian.Write(bytes, 0x06, (ushort)Type);
        BigEndian.Write(bytes, 0x08, (ulong)Size);
        Hash.CopyTo(bytes, 0x10);
        return bytes;
    }
}
