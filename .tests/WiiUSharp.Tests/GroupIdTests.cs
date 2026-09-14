namespace WiiUSharp.Tests;

[TestClass]
public class GroupIdTests
{
    [TestMethod]
    public void EqualityIsByValue()
    {
        Assert.AreEqual(new GroupId(7), new GroupId(7));
        Assert.IsTrue(new GroupId(7) == new GroupId(7));
        Assert.IsTrue(new GroupId(7) != new GroupId(8));
    }

    [TestMethod]
    public void ParsesTextualForm()
    {
        Assert.AreEqual(new GroupId(0x1234), GroupId.Parse("00001234"));
        Assert.AreEqual(new GroupId(0xABCD), GroupId.Parse("0000abcd"));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("1234")]
    [DataRow("000012345")]
    [DataRow("0000123G")]
    public void RejectsMalformedText(string? text)
    {
        Assert.IsFalse(GroupId.TryParse(text, out _));
        Assert.ThrowsExactly<FormatException>(() => GroupId.Parse(text!));
    }

    [TestMethod]
    public void WritesEightUpperCaseHexDigits()
    {
        Assert.AreEqual("00001234", new GroupId(0x1234).ToString());
        Assert.AreEqual("0000ABCD", new GroupId(0xABCD).ToString());
    }
}
