namespace WiiUSharp.Nus.Tests;

[TestClass]
public class FstTests
{
    private string _root = null!;
    private byte[] _fst = null!;

    [TestInitialize]
    public void Initialize()
    {
        _root = Reference.TempRoot();
        var output = Path.Combine(_root, "out");
        new NusPacker(Reference.CommonKey).Pack(FakeTitle.Create(Path.Combine(_root, "title")), output, Reference.TitleKey);
        _fst = Reference.AesCbc(Reference.TitleKey.ToArray(), new byte[16], File.ReadAllBytes(Path.Combine(output, NusFormat.ContentFileName(0))), encrypt: false);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, recursive: true);

    [TestMethod]
    public void Parse_PackedFst_ListsEveryFileWithContentAndLength()
    {
        var fst = Fst.Parse(_fst);

        Assert.AreEqual(9, fst.ContentCount);
        CollectionAssert.AreEquivalent(FakeTitle.Files.Keys.Select(k => "/" + k).ToArray(), fst.Files.Select(f => f.Path).ToArray());
        foreach (var file in fst.Files)
        {
            Assert.AreEqual(FakeTitle.Files[file.Path.TrimStart('/')].Length, file.Length, file.Path);
            Assert.IsTrue(file.ContentIndex > 0 && file.ContentIndex < 9, file.Path);
            Assert.AreEqual(0, file.Offset % NusFormat.ContentAlignment, file.Path);
        }
        Assert.AreEqual(ContentRules.ContentFlags, fst.Files.Single(f => f.Path == "/content/a.bin").Flags);
    }

    [TestMethod]
    public void Parse_FilesSharingAContent_HaveDistinctAlignedOffsets()
    {
        var fst = Fst.Parse(_fst);

        var content = fst.Files.Where(f => f.Path.StartsWith("/content/", StringComparison.Ordinal)).OrderBy(f => f.Offset).ToArray();
        Assert.IsTrue(content.All(f => f.ContentIndex == content[0].ContentIndex));
        for (var i = 1; i < content.Length; i++)
            Assert.IsTrue(content[i].Offset >= content[i - 1].Offset + content[i - 1].Length);
    }

    [TestMethod]
    public void Parse_UnshiftedFlag_KeepsOffsetAsStored()
    {
        var bytes = (byte[])_fst.Clone();
        var entriesAt = 0x20 + 9 * 0x20;
        var at = entriesAt + 0x10;
        while ((bytes[at] & 0x01) != 0)
            at += 0x10;
        bytes[at + 0x0C] = 0x00;
        bytes[at + 0x0D] = (byte)Fst.UnshiftedOffsetFlag;
        bytes[at + 0x04] = 0;
        bytes[at + 0x05] = 0;
        bytes[at + 0x06] = 0;
        bytes[at + 0x07] = 0x30;

        var file = Fst.Parse(bytes).Files[0];

        Assert.AreEqual(0x30, file.Offset);
        Assert.AreEqual(Fst.UnshiftedOffsetFlag, file.Flags);
    }

    [TestMethod]
    public void HasMagic_FstAndOtherBytes_TellsThemApart()
    {
        Assert.IsTrue(Fst.HasMagic(_fst));
        Assert.IsTrue(Fst.HasMagic(new byte[] { 0x46, 0x53, 0x54, 0x00 }));
        Assert.IsFalse(Fst.HasMagic(new byte[] { 0x46, 0x53, 0x54, 0x01 }));
        Assert.IsFalse(Fst.HasMagic(new byte[] { 0x46, 0x53 }));
        Assert.ThrowsExactly<ArgumentNullException>(() => Fst.HasMagic(null!));
    }

    [TestMethod]
    public void Parse_BadBytes_ThrowsInvalidDataException()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => Fst.Parse(new byte[0x8000]));

        var tooManyContents = (byte[])_fst.Clone();
        tooManyContents[0x0B] = 0xFF;
        tooManyContents[0x0A] = 0xFF;
        Assert.ThrowsExactly<InvalidDataException>(() => Fst.Parse(tooManyContents));

        var tooManyEntries = (byte[])_fst.Clone();
        tooManyEntries[0x20 + 9 * 0x20 + 0x08] = 0x7F;
        Assert.ThrowsExactly<InvalidDataException>(() => Fst.Parse(tooManyEntries));

        Assert.ThrowsExactly<ArgumentNullException>(() => Fst.Parse(null!));
    }
}
