namespace WiiUSharp.Tests;

[TestClass]
public class TitleIdTests
{
    [TestMethod]
    public void ComposesFromTypeAndUniqueId()
    {
        var id = new TitleId(TitleType.Game, 0xABCDEF01);

        Assert.AreEqual(0x00050000ABCDEF01ul, id.Value);
    }

    [TestMethod]
    public void EqualityIsByValue()
    {
        Assert.AreEqual(new TitleId(1), new TitleId(1));
        Assert.IsTrue(new TitleId(1) == new TitleId(1));
        Assert.IsTrue(new TitleId(1) != new TitleId(2));
        Assert.AreEqual(new TitleId(1).GetHashCode(), new TitleId(1).GetHashCode());
    }

    [TestMethod]
    public void ParsesTextualForm()
    {
        Assert.AreEqual(new TitleId(0x0005000212345678), TitleId.Parse("0005000212345678"));
        Assert.AreEqual(new TitleId(0x0005000212345678), TitleId.Parse("0005000212345678".ToLowerInvariant()));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("00050002")]
    [DataRow("00050002123456789")]
    [DataRow("0005000212345G78")]
    [DataRow("0x05000212345678")]
    public void RejectsMalformedText(string? text)
    {
        Assert.IsFalse(TitleId.TryParse(text, out _));
        Assert.ThrowsExactly<FormatException>(() => TitleId.Parse(text!));
    }

    [TestMethod]
    public void SplitsIntoTypeAndUniqueId()
    {
        var id = new TitleId(0x0005000212345678);

        Assert.AreEqual(TitleType.Demo, id.Type);
        Assert.AreEqual(0x12345678u, id.UniqueId);
    }

    [TestMethod]
    public void WritesSixteenUpperCaseHexDigits()
    {
        Assert.AreEqual("0005000212345678", new TitleId(0x0005000212345678).ToString());
        Assert.AreEqual("0005000000000ABC", new TitleId(TitleType.Game, 0xABC).ToString());
    }
}
