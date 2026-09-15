using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace WiiUSharp;

/// <summary>
/// Shared plumbing for the XML files a title ships.
/// </summary>
public abstract class TitleXml
{
    /// <summary>
    /// Width of app.xml's title_version field in bytes; hexBinary there, unsignedInt in meta.xml.
    /// </summary>
    public const int TitleVersionBytes = 2;
    /// <summary>
    /// Width of the region field in bytes; it is hexBinary, not a number.
    /// </summary>
    public const int RegionBytes = 4;

    private protected TitleXml(XDocument document, string rootName)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        if (document.Root?.Name.LocalName != rootName)
            throw new InvalidDataException($"Expected a <{rootName}> document.");
    }

    /// <summary>
    /// Underlying document.
    /// </summary>
    public XDocument Document { get; }

    /// <summary>
    /// Text of a top-level element.
    /// </summary>
    /// <param name="element">Element name, e.g. reserved_flag2.</param>
    /// <exception cref="InvalidDataException">Element missing.</exception>
    public string Get(string element) => Element(element).Value;

    /// <summary>
    /// Writes to a file with the XML declaration kept.
    /// </summary>
    /// <param name="path">Destination.</param>
    public void Save(string path)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        Save(stream);
    }

    /// <summary>
    /// Writes to a stream with the XML declaration kept.
    /// </summary>
    /// <param name="stream">Destination.</param>
    public void Save(Stream stream)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            Indent = false,
            NewLineHandling = NewLineHandling.None,
        };
        using var writer = XmlWriter.Create(stream, settings);
        Document.Save(writer);
    }

    /// <summary>
    /// Sets the text of a top-level element.
    /// </summary>
    /// <param name="element">Element name.</param>
    /// <param name="value">New text.</param>
    /// <exception cref="InvalidDataException">Element missing.</exception>
    public void Set(string element, string value) => Element(element).Value = value ?? throw new ArgumentNullException(nameof(value));

    private protected static XDocument LoadDocument(Stream stream) => XDocument.Load(stream, LoadOptions.PreserveWhitespace);

    private protected static XDocument LoadDocument(string path)
    {
        using var stream = File.OpenRead(path);
        return LoadDocument(stream);
    }

    private protected uint GetHex(string element) =>
        uint.TryParse(Get(element), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidDataException($"<{element}> is not hexadecimal.");

    private protected uint GetUInt32(string element) =>
        uint.TryParse(Get(element), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidDataException($"<{element}> is not a number.");

    private protected void SetHex(string element, uint value, int bytes) =>
        Set(element, value.ToString("X" + (bytes * 2).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));

    private protected void SetUInt32(string element, uint value) => Set(element, value.ToString(CultureInfo.InvariantCulture));

    private XElement Element(string name) =>
        Document.Root!.Element(name) ?? throw new InvalidDataException($"<{Document.Root.Name.LocalName}> has no <{name}>.");
}
