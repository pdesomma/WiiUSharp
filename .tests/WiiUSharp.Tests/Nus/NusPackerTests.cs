namespace WiiUSharp.Nus.Tests;

[TestClass]
public class NusPackerTests
{
    private string _root = null!;
    private string _title = null!;
    private string _output = null!;

    [TestInitialize]
    public void Initialize()
    {
        _root = Reference.TempRoot();
        _title = FakeTitle.Create(Path.Combine(_root, "title"));
        _output = Path.Combine(_root, "out");
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, recursive: true);

    [TestMethod]
    public void Pack_FakeTitle_WritesEveryFileTheTmdDescribes()
    {
        var package = new NusPacker(Reference.CommonKey).Pack(_title, _output, Reference.TitleKey);

        Assert.AreEqual(9, package.Contents.Count);
        foreach (var file in package.Files)
            Assert.IsTrue(File.Exists(file), file);
        Assert.AreEqual(package.Files.Count(), Directory.GetFiles(_output).Length);

        var tmd = File.ReadAllBytes(Path.Combine(_output, "title.tmd"));
        Assert.AreEqual(9, Reference.ReadUInt16(tmd, 0x1DE));
        Assert.AreEqual(0x000500021ABCDE00UL, Reference.ReadUInt64(tmd, Tmd.TitleIdOffset));
        Assert.AreEqual(0x1ABC, Reference.ReadUInt16(tmd, 0x198));
        Assert.AreEqual(0x0011, Reference.ReadUInt16(tmd, 0x1DC));
        for (var i = 0; i < 9; i++)
        {
            var record = Reference.Slice(tmd, Tmd.ContentRecordsOffset + i * NusFormat.ContentRecordSize, NusFormat.ContentRecordSize);
            var app = File.ReadAllBytes(Path.Combine(_output, NusFormat.ContentFileName(i)));
            Assert.AreEqual((ulong)app.Length, Reference.ReadUInt64(record, 8), $"content {i} size");
            var hashed = (Reference.ReadUInt16(record, 6) & 0x0002) != 0;
            var expectedHash = hashed
                ? Reference.Sha1(File.ReadAllBytes(Path.Combine(_output, NusFormat.HashFileName(i))))
                : Reference.Sha1(Reference.AesCbc(Reference.TitleKey.ToArray(), Reference.IndexIv(i), app, encrypt: false));
            CollectionAssert.AreEqual(expectedHash, Reference.Slice(record, 0x10, 20), $"content {i} hash");
        }

        var ticket = File.ReadAllBytes(Path.Combine(_output, "title.tik"));
        Assert.AreEqual(0x000500021ABCDE00UL, Reference.ReadUInt64(ticket, Ticket.TitleIdOffset));
        CollectionAssert.AreEqual(CertificateChain.Stub(), File.ReadAllBytes(Path.Combine(_output, "title.cert")));
    }

    [TestMethod]
    public void Pack_FakeTitle_EveryFileReadsBackThroughTheFst()
    {
        new NusPacker(Reference.CommonKey).Pack(_title, _output, Reference.TitleKey);

        var reader = new PackageReader(_output, Reference.TitleKey);
        foreach (var pair in FakeTitle.Files)
            CollectionAssert.AreEqual(pair.Value, reader.Read("/" + pair.Key), pair.Key);
    }

    [TestMethod]
    public void Pack_CustomCertificate_WritesItVerbatim()
    {
        var chain = Reference.Pattern(NusFormat.CertificateChainSize, 9);

        new NusPacker(Reference.CommonKey).Pack(_title, _output, Reference.TitleKey, certificateChain: chain);

        CollectionAssert.AreEqual(chain, File.ReadAllBytes(Path.Combine(_output, "title.cert")));
    }

    [TestMethod]
    public void Pack_WrongCertificateLength_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new NusPacker(Reference.CommonKey).Pack(_title, _output, Reference.TitleKey, certificateChain: new byte[10]));
    }

    [TestMethod]
    public void Pack_MissingMetaFolder_ThrowsDirectoryNotFoundException()
    {
        Directory.Delete(Path.Combine(_title, "meta"), recursive: true);

        Assert.ThrowsExactly<DirectoryNotFoundException>(() => new NusPacker(Reference.CommonKey).Pack(_title, _output, Reference.TitleKey));
    }

    [TestMethod]
    public void Pack_BlankDirectories_ThrowsArgumentException()
    {
        var packer = new NusPacker(Reference.CommonKey);

        Assert.ThrowsExactly<ArgumentException>(() => packer.Pack("", _output, Reference.TitleKey));
        Assert.ThrowsExactly<ArgumentException>(() => packer.Pack(_title, " ", Reference.TitleKey));
    }

    [TestMethod]
    public void Pack_Progress_ReportsEachContent()
    {
        var messages = new List<string>();

        new NusPacker(Reference.CommonKey).Pack(_title, _output, Reference.TitleKey, progress: new SyncProgress<string>(messages.Add));

        Assert.AreEqual(9, messages.Count);
        StringAssert.StartsWith(messages[0], "Packing content 00000001");
        Assert.AreEqual("Packing the FST", messages[8]);
    }

    [TestMethod]
    public void TitleInfo_FromAppXml_ReadsEveryField()
    {
        var info = TitleInfo.FromAppXml(Path.Combine(_title, "code", "app.xml"));

        Assert.AreEqual(0x000500021ABCDE00UL, info.TitleId.Value);
        Assert.AreEqual(0x1ABC, info.GroupId);
        Assert.AreEqual(0x0011, info.TitleVersion);
        Assert.AreEqual(0x000500101000400AUL, info.OsVersion);
        Assert.AreEqual(0x80000000u, info.AppType);
        Assert.AreEqual(0x000500001ABCDE00UL, info.ParentTitleId);
    }

    [TestMethod]
    public void TitleInfo_FromAppXmlWithoutOptionalElements_UsesDefaults()
    {
        var path = Path.Combine(_root, "app.xml");
        File.WriteAllText(path, "<?xml version=\"1.0\" encoding=\"utf-8\"?><app><title_id type=\"hexBinary\" length=\"8\">000500021ABCDE00</title_id><group_id type=\"hexBinary\" length=\"4\">00001ABC</group_id><title_version type=\"hexBinary\" length=\"2\"></title_version></app>");

        var info = TitleInfo.FromAppXml(path);

        Assert.AreEqual(0, info.TitleVersion);
        Assert.AreEqual(TitleInfo.DefaultOsVersion, info.OsVersion);
        Assert.AreEqual(TitleInfo.DefaultAppType, info.AppType);
    }
}
