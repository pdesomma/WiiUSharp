using System.Globalization;

namespace WiiUSharp;

/// <summary>
/// 64-bit title identifier, written as sixteen hex digits.
/// </summary>
public readonly struct TitleId : IEquatable<TitleId>
{
    /// <summary>
    /// Hex digits in the textual form.
    /// </summary>
    public const int Length = 16;

    /// <summary>
    /// Creates a new instance of the <see cref="TitleId"/> struct.
    /// </summary>
    /// <param name="type">High 32 bits.</param>
    /// <param name="uniqueId">Low 32 bits.</param>
    public TitleId(TitleType type, uint uniqueId)
        : this(((ulong)(uint)type << 32) | uniqueId)
    {
    }

    /// <summary>
    /// Creates a new instance of the <see cref="TitleId"/> struct.
    /// </summary>
    /// <param name="value">Raw 64-bit value.</param>
    public TitleId(ulong value)
    {
        Value = value;
    }

    /// <summary>
    /// High 32 bits.
    /// </summary>
    public TitleType Type => (TitleType)(uint)(Value >> 32);
    /// <summary>
    /// Low 32 bits.
    /// </summary>
    public uint UniqueId => (uint)Value;
    /// <summary>
    /// Raw 64-bit value.
    /// </summary>
    public ulong Value { get; }

    /// <inheritdoc/>
    public bool Equals(TitleId other) => Value == other.Value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is TitleId other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(TitleId left, TitleId right) => left.Equals(right);

    public static bool operator !=(TitleId left, TitleId right) => !left.Equals(right);

    /// <summary>
    /// Parses sixteen hex digits.
    /// </summary>
    /// <param name="text">Text to parse.</param>
    /// <exception cref="FormatException">Not sixteen hex digits.</exception>
    public static TitleId Parse(string text)
    {
        if (!TryParse(text, out var id))
            throw new FormatException($"'{text}' is not a title ID; expected {Length} hex digits.");
        return id;
    }

    /// <summary>
    /// Sixteen upper-case hex digits.
    /// </summary>
    public override string ToString() => Value.ToString("X16", CultureInfo.InvariantCulture);

    /// <summary>
    /// Parses sixteen hex digits without throwing.
    /// </summary>
    /// <param name="text">Text to parse.</param>
    /// <param name="id">Parsed value, or default on failure.</param>
    public static bool TryParse(string? text, out TitleId id)
    {
        id = default;
        if (text is null || text.Length != Length)
            return false;
        if (!ulong.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
            return false;
        id = new TitleId(value);
        return true;
    }
}
