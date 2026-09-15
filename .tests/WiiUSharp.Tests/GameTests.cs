namespace WiiUSharp.Tests;

[TestClass]
public class GameTests
{
    [TestMethod]
    public void DefaultsToRegionFreeNintendoTitle()
    {
        var game = NewGame();

        Assert.AreEqual(Region.All, game.Region);
        Assert.AreEqual(Game.NintendoCompanyCode, game.CompanyCode);
        Assert.AreEqual((ushort)0, game.TitleVersion);
        Assert.IsNull(game.GamePadUse, "leaves the base's drc_use alone");
        Assert.AreEqual(0, game.Names.Count);
    }

    [TestMethod]
    public void ForAllLanguagesCoversEveryLanguage()
    {
        var names = LocalizedName.ForAllLanguages(new LocalizedName("Short", "Long"));

        foreach (Language language in Enum.GetValues(typeof(Language)))
        {
            Assert.AreEqual("Short", names[language].ShortName);
            Assert.AreEqual("Long", names[language].LongName);
        }
        Assert.AreEqual(12, names.Count);
    }

    [TestMethod]
    public void NameInReturnsNullForMissingLanguage()
    {
        var game = NewGame();

        Assert.IsNull(game.NameIn(Language.English));
    }

    [TestMethod]
    public void NameInReturnsProvidedName()
    {
        var name = new LocalizedName("Super Metroid");
        var game = new Game(NewGame().TitleId, NewGame().GroupId, NewGame().ProductCode)
        {
            Names = new Dictionary<Language, LocalizedName> { [Language.English] = name },
        };

        Assert.AreSame(name, game.NameIn(Language.English));
        Assert.IsNull(game.NameIn(Language.Japanese));
    }

    [TestMethod]
    public void SingleArgumentNameIsUsedForBothForms()
    {
        var name = new LocalizedName("Earthbound");

        Assert.AreEqual("Earthbound", name.ShortName);
        Assert.AreEqual("Earthbound", name.LongName);
    }

    private static Game NewGame() => new(
        new TitleId(TitleType.Demo, 0x12345678),
        new GroupId(0x5678),
        new ProductCode(ProductCode.EShop, "FAAE"));
}
