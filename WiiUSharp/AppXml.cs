using System.Xml.Linq;

namespace WiiUSharp;

/// <summary>
/// code/app.xml: what the loader checks before running a title.
/// </summary>
public sealed class AppXml : TitleXml
{
    /// <summary>
    /// Root element name.
    /// </summary>
    public const string RootName = "app";

    private AppXml(XDocument document) : base(document, RootName)
    {
    }

    /// <summary>
    /// Writes the game's title ID, group ID and version.
    /// </summary>
    /// <param name="game">Values to write.</param>
    public void Apply(Game game)
    {
        if (game is null)
            throw new ArgumentNullException(nameof(game));

        Set("title_id", game.TitleId.ToString());
        Set("group_id", game.GroupId.ToString());
        SetHex("title_version", game.TitleVersion, TitleVersionBytes);
    }

    /// <summary>
    /// Loads an app.xml file.
    /// </summary>
    /// <param name="path">File path.</param>
    public static AppXml Load(string path) => new(LoadDocument(path));

    /// <summary>
    /// Loads an app.xml document from a stream.
    /// </summary>
    /// <param name="stream">Source.</param>
    public static AppXml Load(Stream stream) => new(LoadDocument(stream));

    /// <summary>
    /// Title ID the loader will use.
    /// </summary>
    public TitleId ReadTitleId() => TitleId.Parse(Get("title_id"));
}
