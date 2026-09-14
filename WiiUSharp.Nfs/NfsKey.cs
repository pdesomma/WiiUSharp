namespace WiiUSharp.Nfs;

/// <summary>
/// AES-128 key that encrypts the container; shipped as code/htk.bin.
/// </summary>
public readonly struct NfsKey : IEquatable<NfsKey>
{
    private readonly byte[] _bytes;

    /// <summary>
    /// Creates a new instance of the <see cref="NfsKey"/> struct.
    /// </summary>
    /// <param name="bytes">Sixteen key bytes.</param>
    /// <exception cref="ArgumentException">Not sixteen bytes.</exception>
    public NfsKey(byte[] bytes)
    {
        if (bytes is null)
            throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length != NfsFormat.KeySize)
            throw new ArgumentException($"Key must be {NfsFormat.KeySize} bytes.", nameof(bytes));

        _bytes = (byte[])bytes.Clone();
    }

    /// <inheritdoc/>
    public bool Equals(NfsKey other) => ToArray().SequenceEqual(other.ToArray());

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is NfsKey other && Equals(other);

    /// <summary>
    /// Reads a key file such as htk.bin.
    /// </summary>
    /// <param name="path">Key file path.</param>
    public static NfsKey FromFile(string path) => new(File.ReadAllBytes(path));

    /// <inheritdoc/>
    public override int GetHashCode() => BitConverter.ToInt32(ToArray(), 0);

    public static bool operator ==(NfsKey left, NfsKey right) => left.Equals(right);

    public static bool operator !=(NfsKey left, NfsKey right) => !left.Equals(right);

    /// <summary>
    /// Copy of the key bytes.
    /// </summary>
    public byte[] ToArray() => (byte[])(_bytes ?? new byte[NfsFormat.KeySize]).Clone();
}
