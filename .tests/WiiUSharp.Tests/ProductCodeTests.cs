namespace WiiUSharp.Tests;

[TestClass]
public class ProductCodeTests
{
    [TestMethod]
    public void EqualityIsByValue()
    {
        Assert.AreEqual(ProductCode.Parse("WUP-N-FAAE"), new ProductCode('N', "FAAE"));
        Assert.IsTrue(ProductCode.Parse("WUP-N-FAAE") != ProductCode.Parse("WUP-P-FAAE"));
        Assert.IsTrue(ProductCode.Parse("WUP-N-FAAE") != ProductCode.Parse("WUP-N-FAAF"));
    }

    [TestMethod]
    public void ParsesTextualForm()
    {
        var code = ProductCode.Parse("WUP-N-A1B2");

        Assert.AreEqual('N', code.Category);
        Assert.AreEqual("A1B2", code.Id);
    }

    [TestMethod]
    [DataRow('n', "FAAE")]
    [DataRow('1', "FAAE")]
    [DataRow('N', "FAA")]
    [DataRow('N', "faae")]
    [DataRow('N', "FA-E")]
    public void RejectsMalformedParts(char category, string id)
    {
        Assert.ThrowsExactly<ArgumentException>(() => new ProductCode(category, id));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("WUP-N-FAA")]
    [DataRow("WUP-N-FAAEE")]
    [DataRow("WUP-N-faae")]
    [DataRow("WUP-n-FAAE")]
    [DataRow("WUP-NN-FAAE")]
    [DataRow("RVL-N-FAAE")]
    [DataRow("WUP-N-FA-E")]
    [DataRow("WUPNFAAE")]
    public void RejectsMalformedText(string? text)
    {
        Assert.IsFalse(ProductCode.TryParse(text, out _));
        Assert.ThrowsExactly<FormatException>(() => ProductCode.Parse(text!));
    }

    [TestMethod]
    public void RejectsNullId()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ProductCode('N', null!));
    }

    [TestMethod]
    public void WritesPlatformCategoryAndId()
    {
        Assert.AreEqual("WUP-N-FAAE", new ProductCode(ProductCode.EShop, "FAAE").ToString());
        Assert.AreEqual("WUP-P-ARKE", new ProductCode(ProductCode.Retail, "ARKE").ToString());
    }
}
