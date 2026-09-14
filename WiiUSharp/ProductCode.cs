namespace WiiUSharp;

/// <summary>
/// Product code in the form WUP-X-XXXX.
/// </summary>
public readonly struct ProductCode : IEquatable<ProductCode>
{
    /// <summary>
    /// Category letter for eShop titles.
    /// </summary>
    public const char EShop = 'N';
    /// <summary>
    /// Platform tag.
    /// </summary>
    public const string Platform = "WUP";
    /// <summary>
    /// Category letter for retail titles.
    /// </summary>
    public const char Retail = 'P';
    private const int IdLength = 4;

    /// <summary>
    /// Creates a new instance of the <see cref="ProductCode"/> struct.
    /// </summary>
    /// <param name="category">Upper-case category letter.</param>
    /// <param name="id">Four upper-case alphanumeric characters.</param>
    /// <exception cref="ArgumentException">Malformed category or id.</exception>
    public ProductCode(char category, string id)
    {
        if (!IsUpperLetter(category))
            throw new ArgumentException("Category must be an upper-case letter.", nameof(category));
        if (id is null)
            throw new ArgumentNullException(nameof(id));
        if (!IsValidId(id))
            throw new ArgumentException($"Id must be exactly {IdLength} upper-case alphanumeric characters.", nameof(id));

        Category = category;
        Id = id;
    }

    /// <summary>
    /// Category letter.
    /// </summary>
    public char Category { get; }
    /// <summary>
    /// Four-character id.
    /// </summary>
    public string Id { get; }

    /// <inheritdoc/>
    public bool Equals(ProductCode other) => Category == other.Category && Id == other.Id;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ProductCode other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => (Category, Id).GetHashCode();

    public static bool operator ==(ProductCode left, ProductCode right) => left.Equals(right);

    public static bool operator !=(ProductCode left, ProductCode right) => !left.Equals(right);

    /// <summary>
    /// Parses WUP-X-XXXX.
    /// </summary>
    /// <param name="text">Text to parse.</param>
    /// <exception cref="FormatException">Not WUP-X-XXXX.</exception>
    public static ProductCode Parse(string text)
    {
        if (!TryParse(text, out var code))
            throw new FormatException($"'{text}' is not a product code; expected {Platform}-X-XXXX.");
        return code;
    }

    /// <summary>
    /// WUP-X-XXXX.
    /// </summary>
    public override string ToString() => $"{Platform}-{Category}-{Id}";

    /// <summary>
    /// Parses WUP-X-XXXX without throwing.
    /// </summary>
    /// <param name="text">Text to parse.</param>
    /// <param name="code">Parsed value, or default on failure.</param>
    public static bool TryParse(string? text, out ProductCode code)
    {
        code = default;
        if (text is null)
            return false;

        var parts = text.Split('-');
        if (parts.Length != 3 || parts[0] != Platform || parts[1].Length != 1)
            return false;

        var category = parts[1][0];
        if (!IsUpperLetter(category) || !IsValidId(parts[2]))
            return false;

        code = new ProductCode(category, parts[2]);
        return true;
    }

    private static bool IsUpperAlphanumeric(char c) => IsUpperLetter(c) || (c >= '0' && c <= '9');

    private static bool IsUpperLetter(char c) => c >= 'A' && c <= 'Z';

    private static bool IsValidId(string id) => id.Length == IdLength && id.All(IsUpperAlphanumeric);
}
