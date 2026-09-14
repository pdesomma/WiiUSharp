using System.Globalization;

namespace WiiUSharp.Nus;

/// <summary>
/// Values the TMD and ticket carry, normally read from code/app.xml.
/// </summary>
public sealed class TitleInfo
{
    /// <summary>
    /// App type assumed when app.xml has none.
    /// </summary>
    public const uint DefaultAppType = 0x80000000;
    /// <summary>
    /// OS version assumed when app.xml has none.
    /// </summary>
    public const ulong DefaultOsVersion = 0x000500101000400A;

    /// <summary>
    /// Creates a new instance of the <see cref="TitleInfo"/> class.
    /// </summary>
    /// <param name="titleId">Title ID.</param>
    /// <param name="groupId">Low sixteen bits of the group ID.</param>
    /// <param name="titleVersion">Title version.</param>
    /// <param name="osVersion">Required OS version.</param>
    /// <param name="appType">App type.</param>
    public TitleInfo(TitleId titleId, ushort groupId, ushort titleVersion = 0, ulong osVersion = DefaultOsVersion, uint appType = DefaultAppType)
    {
        TitleId = titleId;
        GroupId = groupId;
        TitleVersion = titleVersion;
        OsVersion = osVersion;
        AppType = appType;
    }

    /// <summary>
    /// App type.
    /// </summary>
    public uint AppType { get; }
    /// <summary>
    /// Low sixteen bits of the group ID.
    /// </summary>
    public ushort GroupId { get; }
    /// <summary>
    /// Required OS version.
    /// </summary>
    public ulong OsVersion { get; }
    /// <summary>
    /// Title ID with the type nibble cleared; parent of the content folder.
    /// </summary>
    public ulong ParentTitleId => TitleId.Value & ~0x0000000F00000000UL;
    /// <summary>
    /// Title ID.
    /// </summary>
    public TitleId TitleId { get; }
    /// <summary>
    /// Title version.
    /// </summary>
    public ushort TitleVersion { get; }

    /// <summary>
    /// Reads title_id, group_id, title_version, os_version and app_type; missing or empty optional elements take the defaults.
    /// </summary>
    /// <param name="appXml">Loaded app.xml.</param>
    public static TitleInfo FromAppXml(AppXml appXml)
    {
        if (appXml is null)
            throw new ArgumentNullException(nameof(appXml));

        return new TitleInfo(
            appXml.ReadTitleId(),
            (ushort)Hex(appXml, "group_id", 0),
            (ushort)Hex(appXml, "title_version", 0),
            Hex(appXml, "os_version", DefaultOsVersion),
            (uint)Hex(appXml, "app_type", DefaultAppType));
    }

    /// <summary>
    /// Reads an app.xml file.
    /// </summary>
    /// <param name="path">File path.</param>
    public static TitleInfo FromAppXml(string path) => FromAppXml(AppXml.Load(path));

    private static ulong Hex(AppXml appXml, string element, ulong fallback)
    {
        string text;
        try
        {
            text = appXml.Get(element);
        }
        catch (InvalidDataException)
        {
            return fallback;
        }
        return text.Length == 0 ? fallback : ulong.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }
}
