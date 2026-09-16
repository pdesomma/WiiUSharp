using WiiUSharp.Nus;

namespace WiiUSharp.Wud;

/// <summary>
/// The per-disc AES key that unlocks a disc image's partition table and system partition; dumps ship it as game.key.
/// </summary>
public readonly struct DiscKey : IEquatable<DiscKey>
{
    private readonly byte[] _bytes;

    /// <summary>
    /// Creates a new instance of the <see cref="DiscKey"/> struct.
    /// </summary>
    /// <param name="bytes">Sixteen key bytes.</param>
    public DiscKey(byte[] bytes)
    {
        _bytes = KeyBytes.Validate(bytes);
    }

    /// <summary>
    /// The game.key that sits beside an image, if any.
    /// </summary>
    /// <param name="imagePath">Disc image path.</param>
    /// <returns>Null when the folder holds no game.key.</returns>
    public static DiscKey? Beside(string imagePath)
    {
        if (imagePath is null)
            throw new ArgumentNullException(nameof(imagePath));

        var folder = Path.GetDirectoryName(Path.GetFullPath(imagePath));
        var path = Path.Combine(folder ?? "", WudFormat.DiscKeyFileName);
        return File.Exists(path) ? FromFile(path) : null;
    }

    /// <inheritdoc/>
    public bool Equals(DiscKey other) => ToArray().SequenceEqual(other.ToArray());

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DiscKey other && Equals(other);

    /// <summary>
    /// Reads the sixteen raw bytes of a key file.
    /// </summary>
    /// <param name="path">Key file.</param>
    public static DiscKey FromFile(string path) => new(File.ReadAllBytes(path));

    /// <inheritdoc/>
    public override int GetHashCode() => BitConverter.ToInt32(ToArray(), 0);

    public static bool operator ==(DiscKey left, DiscKey right) => left.Equals(right);

    public static bool operator !=(DiscKey left, DiscKey right) => !left.Equals(right);

    /// <summary>
    /// Parses 32 hex characters.
    /// </summary>
    /// <param name="hex">Key as hex.</param>
    public static DiscKey Parse(string hex) => new(KeyBytes.Parse(hex));

    /// <summary>
    /// Copy of the key bytes.
    /// </summary>
    public byte[] ToArray() => (byte[])(_bytes ?? new byte[NusFormat.KeySize]).Clone();

    /// <inheritdoc/>
    public override string ToString() => KeyBytes.ToHex(ToArray());
}
