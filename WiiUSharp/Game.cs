namespace WiiUSharp;

/// <summary>
/// Metadata the Wii U reads from an installed title.
/// </summary>
public sealed class Game
{
    /// <summary>
    /// Nintendo's company code.
    /// </summary>
    public const string NintendoCompanyCode = "0001";

    /// <summary>
    /// Creates a new instance of the <see cref="Game"/> class.
    /// </summary>
    /// <param name="titleId">The title ID.</param>
    /// <param name="groupId">The group ID.</param>
    /// <param name="productCode">The product code.</param>
    public Game(TitleId titleId, GroupId groupId, ProductCode productCode)
    {
        TitleId = titleId;
        GroupId = groupId;
        ProductCode = productCode;
    }

    /// <summary>
    /// Four-character publisher code.
    /// </summary>
    public string CompanyCode { get; init; } = NintendoCompanyCode;
    /// <summary>
    /// Raw drc_use value; seen as 0, 1 or 65537. Null leaves the base's value alone.
    /// </summary>
    public uint? GamePadUse { get; init; }
    /// <summary>
    /// Group shared with updates and DLC.
    /// </summary>
    public GroupId GroupId { get; }
    /// <summary>
    /// Name per language.
    /// </summary>
    public IReadOnlyDictionary<Language, LocalizedName> Names { get; init; } = new Dictionary<Language, LocalizedName>();
    /// <summary>
    /// Product code.
    /// </summary>
    public ProductCode ProductCode { get; }
    /// <summary>
    /// Regions allowed to run the title.
    /// </summary>
    public Region Region { get; init; } = Region.All;
    /// <summary>
    /// Title ID.
    /// </summary>
    public TitleId TitleId { get; }
    /// <summary>
    /// Title data version.
    /// </summary>
    public ushort TitleVersion { get; init; }

    /// <summary>
    /// Name in the given language, or null.
    /// </summary>
    public LocalizedName? NameIn(Language language) => Names.TryGetValue(language, out var name) ? name : null;
}
