namespace WiiUSharp.Nus;

/// <summary>
/// Wii U common key; encrypts the title key inside the ticket.
/// </summary>
public readonly struct CommonKey : IEquatable<CommonKey>
{
    private readonly byte[] _bytes;

    /// <summary>
    /// Creates a new instance of the <see cref="CommonKey"/> struct.
    /// </summary>
    /// <param name="bytes">Sixteen key bytes.</param>
    /// <exception cref="ArgumentException">Not sixteen bytes.</exception>
    public CommonKey(byte[] bytes)
    {
        _bytes = KeyBytes.Validate(bytes);
    }

    /// <inheritdoc/>
    public bool Equals(CommonKey other) => ToArray().SequenceEqual(other.ToArray());

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CommonKey other && Equals(other);

    /// <summary>
    /// Reads sixteen raw bytes from a file.
    /// </summary>
    /// <param name="path">Key file path.</param>
    public static CommonKey FromFile(string path) => new(File.ReadAllBytes(path));

    /// <inheritdoc/>
    public override int GetHashCode() => BitConverter.ToInt32(ToArray(), 0);

    public static bool operator ==(CommonKey left, CommonKey right) => left.Equals(right);

    public static bool operator !=(CommonKey left, CommonKey right) => !left.Equals(right);

    /// <summary>
    /// Parses thirty-two hex characters.
    /// </summary>
    /// <param name="hex">Key as hex.</param>
    public static CommonKey Parse(string hex) => new(KeyBytes.Parse(hex));

    /// <summary>
    /// Copy of the key bytes.
    /// </summary>
    public byte[] ToArray() => (byte[])(_bytes ?? new byte[NusFormat.KeySize]).Clone();

    /// <summary>
    /// Key as lowercase hex.
    /// </summary>
    public override string ToString() => KeyBytes.ToHex(ToArray());
}
