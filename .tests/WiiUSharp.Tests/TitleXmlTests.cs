using System.Text;

namespace WiiUSharp.Tests;

[TestClass]
public class TitleXmlTests
{
    private static readonly Game SuperMetroid = new(
        new TitleId(TitleType.Demo, 0x1234ABCD),
        new GroupId(0xABCD),
        new ProductCode(ProductCode.EShop, "FAAE"))
    {
        CompanyCode = "0001",
        TitleVersion = 16,
        Region = Region.All,
        GamePadUse = 65537,
        Names = new Dictionary<Language, LocalizedName>
        {
            [Language.English] = new("Super Metroid", "Super\nMetroid"),
            [Language.Japanese] = new("スーパーメトロイド"),
        },
    };

    [TestMethod]
    public void AppXmlApplyWritesIdentityOnly()
    {
        var app = AppXml.Load(Stream(AppXmlText));

        app.Apply(SuperMetroid);

        Assert.AreEqual("000500021234ABCD", app.Get("title_id"));
        Assert.AreEqual("0000ABCD", app.Get("group_id"));
        Assert.AreEqual("16", app.Get("title_version"));
        Assert.AreEqual("0000000000000000", app.Get("os_version"));
        Assert.AreEqual(SuperMetroid.TitleId, app.ReadTitleId());
    }

    [TestMethod]
    public void GetAndSetReachAnyTopLevelElement()
    {
        var meta = MetaXml.Load(Stream(MetaXmlText));

        meta.Set("reserved_flag2", "534D4E45");

        Assert.AreEqual("534D4E45", meta.Get("reserved_flag2"));
        Assert.ThrowsExactly<InvalidDataException>(() => meta.Get("no_such_element"));
        Assert.ThrowsExactly<InvalidDataException>(() => meta.Set("no_such_element", "x"));
    }

    [TestMethod]
    public void LoadRejectsTheWrongRoot()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => MetaXml.Load(Stream(AppXmlText)));
        Assert.ThrowsExactly<InvalidDataException>(() => AppXml.Load(Stream(MetaXmlText)));
    }

    [TestMethod]
    public void MetaXmlApplyWritesEveryModelledFieldAndOnlyProvidedNames()
    {
        var meta = MetaXml.Load(Stream(MetaXmlText));

        meta.Apply(SuperMetroid);

        Assert.AreEqual("000500021234ABCD", meta.Get("title_id"));
        Assert.AreEqual("0000ABCD", meta.Get("group_id"));
        Assert.AreEqual("WUP-N-FAAE", meta.Get("product_code"));
        Assert.AreEqual("0001", meta.Get("company_code"));
        Assert.AreEqual("16", meta.Get("title_version"));
        Assert.AreEqual("4294967295", meta.Get("region"));
        Assert.AreEqual("65537", meta.Get("drc_use"));
        Assert.AreEqual("Super\nMetroid", meta.Get("longname_en"));
        Assert.AreEqual("Super Metroid", meta.Get("shortname_en"));
        Assert.AreEqual("スーパーメトロイド", meta.Get("longname_ja"));
        Assert.AreEqual("Base Title FR", meta.Get("longname_fr"));
        Assert.AreEqual("00000000", meta.Get("reserved_flag2"));
    }

    [TestMethod]
    public void MetaXmlReadRoundTripsThroughApply()
    {
        var meta = MetaXml.Load(Stream(MetaXmlText));
        meta.Apply(SuperMetroid);

        var game = meta.Read();

        Assert.AreEqual(SuperMetroid.TitleId, game.TitleId);
        Assert.AreEqual(SuperMetroid.GroupId, game.GroupId);
        Assert.AreEqual(SuperMetroid.ProductCode, game.ProductCode);
        Assert.AreEqual("0001", game.CompanyCode);
        Assert.AreEqual((ushort)16, game.TitleVersion);
        Assert.AreEqual(Region.All, game.Region);
        Assert.AreEqual(65537u, game.GamePadUse);
        Assert.AreEqual(new LocalizedName("Super Metroid", "Super\nMetroid"), game.NameIn(Language.English));
        Assert.AreEqual("Base Title FR", game.NameIn(Language.French)!.LongName);
        Assert.IsNull(game.NameIn(Language.Korean));
    }

    [TestMethod]
    public void SaveKeepsDeclarationAttributesAndUntouchedFields()
    {
        var meta = MetaXml.Load(Stream(MetaXmlText));
        meta.Apply(SuperMetroid);
        var output = new MemoryStream();

        meta.Save(output);
        var text = Encoding.UTF8.GetString(output.ToArray());

        StringAssert.StartsWith(text, "<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        StringAssert.Contains(text, "<title_id type=\"hexBinary\" length=\"8\">000500021234ABCD</title_id>");
        StringAssert.Contains(text, "<version type=\"unsignedInt\" length=\"4\">33</version>");
        StringAssert.Contains(text, "<longname_en type=\"string\" length=\"512\">Super\nMetroid</longname_en>");

        var reloaded = MetaXml.Load(new MemoryStream(output.ToArray()));
        Assert.AreEqual(SuperMetroid.TitleId, reloaded.Read().TitleId);
    }

    private static MemoryStream Stream(string xml) => new(Encoding.UTF8.GetBytes(xml));

    private const string AppXmlText =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
        "<app type=\"complex\" access=\"777\">\n" +
        "  <version type=\"unsignedInt\" length=\"4\">16</version>\n" +
        "  <os_version type=\"hexBinary\" length=\"8\">0000000000000000</os_version>\n" +
        "  <title_id type=\"hexBinary\" length=\"8\">0005000010000000</title_id>\n" +
        "  <title_version type=\"hexBinary\" length=\"2\">0000</title_version>\n" +
        "  <sdk_version type=\"unsignedInt\" length=\"4\">20909</sdk_version>\n" +
        "  <app_type type=\"hexBinary\" length=\"4\">80000000</app_type>\n" +
        "  <group_id type=\"hexBinary\" length=\"4\">00000000</group_id>\n" +
        "</app>\n";

    private static readonly string MetaXmlText = BuildMetaXml();

    private static string BuildMetaXml()
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
        sb.Append("<menu type=\"complex\" access=\"777\">\n");
        sb.Append("  <version type=\"unsignedInt\" length=\"4\">33</version>\n");
        sb.Append("  <product_code type=\"string\" length=\"32\">WUP-N-BASE</product_code>\n");
        sb.Append("  <company_code type=\"string\" length=\"8\">0001</company_code>\n");
        sb.Append("  <title_version type=\"unsignedShort\" length=\"2\">0</title_version>\n");
        sb.Append("  <title_id type=\"hexBinary\" length=\"8\">0005000010000000</title_id>\n");
        sb.Append("  <group_id type=\"hexBinary\" length=\"4\">00000000</group_id>\n");
        sb.Append("  <region type=\"hexBinary\" length=\"4\">2</region>\n");
        sb.Append("  <drc_use type=\"unsignedInt\" length=\"4\">0</drc_use>\n");
        sb.Append("  <reserved_flag2 type=\"hexBinary\" length=\"4\">00000000</reserved_flag2>\n");
        foreach (Language language in Enum.GetValues(typeof(Language)))
        {
            var code = language.Code();
            var text = language == Language.Korean ? "" : "Base Title " + code.ToUpperInvariant();
            sb.Append($"  <longname_{code} type=\"string\" length=\"512\">{text}</longname_{code}>\n");
            sb.Append($"  <shortname_{code} type=\"string\" length=\"256\">{text}</shortname_{code}>\n");
        }
        sb.Append("</menu>\n");
        return sb.ToString();
    }
}
