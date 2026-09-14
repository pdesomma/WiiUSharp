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
    /// <param name="size">Content length as the TMD records it; plain contents may state the unpadded plaintext length.</param>
    /// <param name="hash">SHA-1 the TMD carries.</param>
    public ContentRecord(int index, ContentType type, long size, byte[] hash)
        : this(index, type, size, hash, (uint)index)
    {
    }

    /// <summary>
    /// Creates a new instance of the <see cref="ContentRecord"/> class with a content ID other than the index.
    /// </summary>
    /// <param name="index">Position in the package.</param>
    /// <param name="type">Type bits.</param>
    /// <param name="size">Content length as the TMD records it.</param>
    /// <param name="hash">SHA-1 the TMD carries.</param>
    /// <param name="id">Content ID; names the file on the CDN and on disk.</param>
    public ContentRecord(int index, ContentType type, long size, byte[] hash, uint id)
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
        Id = id;
    }

    /// <summary>
    /// Length of the .app file: the size rounded up to whole AES blocks.
    /// </summary>
    public long EncryptedSize => (Size + NusFormat.KeySize - 1) / NusFormat.KeySize * NusFormat.KeySize;
    /// <summary>
    /// SHA-1 the TMD carries.
    /// </summary>
    public byte[] Hash { get; }
    /// <summary>
    /// Content ID; names the file on the CDN and on disk. Equals the index in packed output.
    /// </summary>
    public uint Id { get; }
    /// <summary>
    /// Position in the package; the IV of the content.
    /// </summary>
    public int Index { get; }
    /// <summary>
    /// True when the content uses the hashed layout.
    /// </summary>
    public bool IsHashed => (Type & ContentType.Hashed) != 0;
    /// <summary>
    /// Content length as the TMD records it; see <see cref="EncryptedSize"/> for the file length.
    /// </summary>
    public long Size { get; }
    /// <summary>
    /// Type bits.
    /// </summary>
    public ContentType Type { get; }

    /// <summary>
    /// Reads a 0x30-byte TMD record.
    /// </summary>
    /// <param name="bytes">Buffer holding the record.</param>
    /// <param name="offset">Where the record starts.</param>
    /// <exception cref="InvalidDataException">The record's ID and index disagree.</exception>
    public static ContentRecord Parse(byte[] bytes, int offset)
    {
        if (bytes is null)
            throw new ArgumentNullException(nameof(bytes));
        if (offset < 0 || offset + NusFormat.ContentRecordSize > bytes.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        var id = BigEndian.ReadUInt32(bytes, offset);
        var index = BigEndian.ReadUInt16(bytes, offset + 0x04);
        var type = (ContentType)BigEndian.ReadUInt16(bytes, offset + 0x06);
        var size = (long)BigEndian.ReadUInt64(bytes, offset + 0x08);
        var hash = new byte[NusFormat.HashSize];
        Array.Copy(bytes, offset + 0x10, hash, 0, hash.Length);
        return new ContentRecord(index, type, size, hash, id);
    }

    /// <summary>
    /// The 0x30-byte TMD record.
    /// </summary>
    public byte[] ToBytes()
    {
        var bytes = new byte[NusFormat.ContentRecordSize];
        BigEndian.Write(bytes, 0x00, Id);
        BigEndian.Write(bytes, 0x04, (ushort)Index);
        BigEndian.Write(bytes, 0x06, (ushort)Type);
        BigEndian.Write(bytes, 0x08, (ulong)Size);
        Hash.CopyTo(bytes, 0x10);
        return bytes;
    }
}
