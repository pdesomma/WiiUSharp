using System.Xml.Linq;

namespace WiiUSharp;

/// <summary>
/// meta/meta.xml: what the system menu shows and checks for a title.
/// </summary>
public sealed class MetaXml : TitleXml
{
    /// <summary>
    /// Root element name.
    /// </summary>
    public const string RootName = "menu";

    private MetaXml(XDocument document) : base(document, RootName)
    {
    }

    /// <summary>
    /// Writes the game's identity, region, GamePad use and names. Names for languages the game does not provide are left as they are.
    /// </summary>
    /// <param name="game">Values to write.</param>
    public void Apply(Game game)
    {
        if (game is null)
            throw new ArgumentNullException(nameof(game));

        Set("title_id", game.TitleId.ToString());
        Set("group_id", game.GroupId.ToString());
        Set("product_code", game.ProductCode.ToString());
        Set("company_code", game.CompanyCode);
        SetHex("title_version", game.TitleVersion, TitleVersionBytes);
        SetHex("region", (uint)game.Region, RegionBytes);
        SetUInt32("drc_use", game.GamePadUse);
        foreach (var pair in game.Names)
        {
            Set("longname_" + pair.Key.Code(), pair.Value.LongName);
            Set("shortname_" + pair.Key.Code(), pair.Value.ShortName);
        }
    }

    /// <summary>
    /// Loads a meta.xml file.
    /// </summary>
    /// <param name="path">File path.</param>
    public static MetaXml Load(string path) => new(LoadDocument(path));

    /// <summary>
    /// Loads a meta.xml document from a stream.
    /// </summary>
    /// <param name="stream">Source.</param>
    public static MetaXml Load(Stream stream) => new(LoadDocument(stream));

    /// <summary>
    /// Reads the game described by the file; only languages with a non-empty long name are included.
    /// </summary>
    public Game Read()
    {
        var names = new Dictionary<Language, LocalizedName>();
        foreach (Language language in Enum.GetValues(typeof(Language)))
        {
            var longName = Get("longname_" + language.Code());
            if (longName.Length == 0)
                continue;
            names[language] = new LocalizedName(Get("shortname_" + language.Code()), longName);
        }

        return new Game(TitleId.Parse(Get("title_id")), GroupId.Parse(Get("group_id")), ProductCode.Parse(Get("product_code")))
        {
            CompanyCode = Get("company_code"),
            TitleVersion = checked((ushort)GetHex("title_version")),
            Region = (Region)GetHex("region"),
            GamePadUse = GetUInt32("drc_use"),
            Names = names,
        };
    }
}
