namespace WiiUSharp.Tests;

[TestClass]
public class LanguageTests
{
    [TestMethod]
    public void EveryLanguageHasADistinctCode()
    {
        var codes = Enum.GetValues(typeof(Language)).Cast<Language>().Select(l => l.Code()).ToList();

        Assert.AreEqual(12, codes.Count);
        Assert.AreEqual(codes.Count, codes.Distinct().Count());
        CollectionAssert.AreEquivalent(
            new[] { "ja", "en", "fr", "de", "it", "es", "zhs", "ko", "nl", "pt", "ru", "zht" },
            codes);
    }

    [TestMethod]
    public void UnknownLanguageHasNoCode()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ((Language)999).Code());
    }
}
