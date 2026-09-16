namespace WiiUSharp.Nus;

/// <summary>
/// Key every content of a package is encrypted with.
/// </summary>
public readonly struct TitleKey : IEquatable<TitleKey>
{
    private readonly byte[] _bytes;

    /// <summary>
    /// Creates a new instance of the <see cref="TitleKey"/> struct.
    /// </summary>
    /// <param name="bytes">Sixteen key bytes.</param>
    /// <exception cref="ArgumentException">Not sixteen bytes.</exception>
    public TitleKey(byte[] bytes)
    {
        _bytes = KeyBytes.Validate(bytes);
    }

    /// <summary>
    /// Wraps the key the way a ticket stores it.
    /// </summary>
    /// <param name="titleId">Title ID; forms the IV.</param>
    /// <param name="commonKey">Wrapping key.</param>
    public EncryptedTitleKey Encrypt(TitleId titleId, CommonKey commonKey) =>
        new(Ticket.EncryptTitleKey(titleId, this, commonKey));

    /// <inheritdoc/>
    public bool Equals(TitleKey other) => ToArray().SequenceEqual(other.ToArray());

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is TitleKey other && Equals(other);

    /// <summary>
    /// Reads sixteen raw bytes from a file.
    /// </summary>
    /// <param name="path">Key file path.</param>
    public static TitleKey FromFile(string path) => new(File.ReadAllBytes(path));

    /// <inheritdoc/>
    public override int GetHashCode() => BitConverter.ToInt32(ToArray(), 0);

    public static bool operator ==(TitleKey left, TitleKey right) => left.Equals(right);

    public static bool operator !=(TitleKey left, TitleKey right) => !left.Equals(right);

    /// <summary>
    /// Parses thirty-two hex characters.
    /// </summary>
    /// <param name="hex">Key as hex.</param>
    public static TitleKey Parse(string hex) => new(KeyBytes.Parse(hex));

    /// <summary>
    /// Copy of the key bytes.
    /// </summary>
    public byte[] ToArray() => (byte[])(_bytes ?? new byte[NusFormat.KeySize]).Clone();

    /// <summary>
    /// Key as lowercase hex.
    /// </summary>
    public override string ToString() => KeyBytes.ToHex(ToArray());
}
