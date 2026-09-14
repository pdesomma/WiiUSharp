namespace WiiUSharp.Nus;

/// <summary>
/// Title key as the ticket stores it: wrapped with the common key. The form title key lists carry.
/// </summary>
public readonly struct EncryptedTitleKey : IEquatable<EncryptedTitleKey>
{
    private readonly byte[] _bytes;

    /// <summary>
    /// Creates a new instance of the <see cref="EncryptedTitleKey"/> struct.
    /// </summary>
    /// <param name="bytes">Sixteen wrapped key bytes.</param>
    /// <exception cref="ArgumentException">Not sixteen bytes.</exception>
    public EncryptedTitleKey(byte[] bytes)
    {
        _bytes = KeyBytes.Validate(bytes);
    }

    /// <summary>
    /// Unwraps to the plain title key.
    /// </summary>
    /// <param name="titleId">Title ID; forms the IV.</param>
    /// <param name="commonKey">Wrapping key.</param>
    public TitleKey Decrypt(TitleId titleId, CommonKey commonKey)
    {
        using var aes = new Aes128Cbc(commonKey.ToArray());
        return new TitleKey(aes.Decrypt(ToArray(), Aes128Cbc.IvFor(titleId)));
    }

    /// <inheritdoc/>
    public bool Equals(EncryptedTitleKey other) => ToArray().SequenceEqual(other.ToArray());

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is EncryptedTitleKey other && Equals(other);

    /// <summary>
    /// Reads sixteen raw bytes from a file.
    /// </summary>
    /// <param name="path">Key file path.</param>
    public static EncryptedTitleKey FromFile(string path) => new(File.ReadAllBytes(path));

    /// <inheritdoc/>
    public override int GetHashCode() => BitConverter.ToInt32(ToArray(), 0);

    public static bool operator ==(EncryptedTitleKey left, EncryptedTitleKey right) => left.Equals(right);

    public static bool operator !=(EncryptedTitleKey left, EncryptedTitleKey right) => !left.Equals(right);

    /// <summary>
    /// Parses thirty-two hex characters.
    /// </summary>
    /// <param name="hex">Wrapped key as hex.</param>
    public static EncryptedTitleKey Parse(string hex) => new(KeyBytes.Parse(hex));

    /// <summary>
    /// Copy of the wrapped key bytes.
    /// </summary>
    public byte[] ToArray() => (byte[])(_bytes ?? new byte[NusFormat.KeySize]).Clone();

    /// <summary>
    /// Wrapped key as lowercase hex.
    /// </summary>
    public override string ToString() => KeyBytes.ToHex(ToArray());
}
