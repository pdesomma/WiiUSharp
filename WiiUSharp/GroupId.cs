using System.Globalization;

namespace WiiUSharp;

/// <summary>
/// 32-bit group identifier, written as eight hex digits.
/// </summary>
public readonly struct GroupId : IEquatable<GroupId>
{
    /// <summary>
    /// Hex digits in the textual form.
    /// </summary>
    public const int Length = 8;

    /// <summary>
    /// Creates a new instance of the <see cref="GroupId"/> struct.
    /// </summary>
    /// <param name="value">Raw 32-bit value.</param>
    public GroupId(uint value)
    {
        Value = value;
    }

    /// <summary>
    /// Raw 32-bit value.
    /// </summary>
    public uint Value { get; }

    /// <inheritdoc/>
    public bool Equals(GroupId other) => Value == other.Value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is GroupId other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(GroupId left, GroupId right) => left.Equals(right);

    public static bool operator !=(GroupId left, GroupId right) => !left.Equals(right);

    /// <summary>
    /// Parses eight hex digits.
    /// </summary>
    /// <param name="text">Text to parse.</param>
    /// <exception cref="FormatException">Not eight hex digits.</exception>
    public static GroupId Parse(string text)
    {
        if (!TryParse(text, out var id))
            throw new FormatException($"'{text}' is not a group ID; expected {Length} hex digits.");
        return id;
    }

    /// <summary>
    /// Eight upper-case hex digits.
    /// </summary>
    public override string ToString() => Value.ToString("X8", CultureInfo.InvariantCulture);

    /// <summary>
    /// Parses eight hex digits without throwing.
    /// </summary>
    /// <param name="text">Text to parse.</param>
    /// <param name="id">Parsed value, or default on failure.</param>
    public static bool TryParse(string? text, out GroupId id)
    {
        id = default;
        if (text is null || text.Length != Length)
            return false;
        if (!uint.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
            return false;
        id = new GroupId(value);
        return true;
    }
}
