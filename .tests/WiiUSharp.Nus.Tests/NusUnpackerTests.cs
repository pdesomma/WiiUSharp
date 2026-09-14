namespace WiiUSharp.Nus.Tests;

[TestClass]
public class NusUnpackerTests
{
    private static readonly CommonKey WrongKey = new(Enumerable.Range(1, 16).Select(i => (byte)(i * 13)).ToArray());

    private string _root = null!;
    private string _package = null!;
    private string _output = null!;

    [TestInitialize]
    public void Initialize()
    {
        _root = Reference.TempRoot();
        _package = Path.Combine(_root, "package");
        _output = Path.Combine(_root, "unpacked");
        new NusPacker(Reference.CommonKey).Pack(FakeTitle.Create(Path.Combine(_root, "title")), _package, Reference.TitleKey);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, recursive: true);

    [TestMethod]
    public void Unpack_PackedTitle_RestoresEveryFile()
    {
        var reports = new List<string>();

        var package = new NusUnpacker(Reference.CommonKey).Unpack(_package, _output, new SyncProgress<string>(reports.Add));

        foreach (var pair in FakeTitle.Files)
            CollectionAssert.AreEqual(pair.Value, File.ReadAllBytes(Path.Combine(_output, pair.Key.Replace('/', Path.DirectorySeparatorChar))), pair.Key);
        Assert.AreEqual(FakeTitle.Files.Count, Directory.GetFiles(_output, "*", SearchOption.AllDirectories).Length);
        Assert.AreEqual(Reference.TitleId, package.Title.TitleId);
        Assert.AreEqual(9, package.Contents.Count);
        Assert.AreEqual("Reading the FST", reports[0]);
        Assert.IsTrue(reports.Count > 1 && reports.Skip(1).All(r => r.StartsWith("Unpacking content ", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Unpack_ExplicitTitleKey_IgnoresTheTicket()
    {
        File.Delete(Path.Combine(_package, NusFormat.TicketFileName));

        NusUnpacker.Unpack(_package, _output, Reference.TitleKey);

        CollectionAssert.AreEqual(FakeTitle.Files["code/game.rpx"], File.ReadAllBytes(Path.Combine(_output, "code", "game.rpx")));
    }

    [TestMethod]
    public void Unpack_WrongCommonKey_ThrowsBeforeWritingAnything()
    {
        var error = Assert.ThrowsExactly<InvalidDataException>(() => new NusUnpacker(WrongKey).Unpack(_package, _output));

        StringAssert.Contains(error.Message, "FST");
        Assert.IsFalse(Directory.Exists(_output));
    }

    [TestMethod]
    public void Unpack_DamagedHashedContent_ThrowsInvalidDataException()
    {
        var path = Path.Combine(_package, NusFormat.ContentFileName(Hashed()));
        var bytes = File.ReadAllBytes(path);
        bytes[NusFormat.HashBlockHeaderSize + 100] ^= 0xFF;
        File.WriteAllBytes(path, bytes);

        var error = Assert.ThrowsExactly<InvalidDataException>(() => new NusUnpacker(Reference.CommonKey).Unpack(_package, _output));

        StringAssert.Contains(error.Message, "H0");
    }

    [TestMethod]
    public void Unpack_DamagedPlainContent_ThrowsInvalidDataException()
    {
        var path = Path.Combine(_package, NusFormat.ContentFileName(Plain()));
        var bytes = File.ReadAllBytes(path);
        bytes[bytes.Length - 1] ^= 0xFF;
        File.WriteAllBytes(path, bytes);

        Assert.ThrowsExactly<InvalidDataException>(() => new NusUnpacker(Reference.CommonKey).Unpack(_package, _output));
    }

    [TestMethod]
    public void Unpack_H3Mismatch_ThrowsInvalidDataException()
    {
        var path = Path.Combine(_package, NusFormat.HashFileName(Hashed()));
        var h3 = File.ReadAllBytes(path);
        h3[0] ^= 0xFF;
        File.WriteAllBytes(path, h3);

        var error = Assert.ThrowsExactly<InvalidDataException>(() => new NusUnpacker(Reference.CommonKey).Unpack(_package, _output));

        StringAssert.Contains(error.Message, "H3");
    }

    [TestMethod]
    public void Unpack_MissingFiles_ThrowsFileNotFoundException()
    {
        File.Delete(Path.Combine(_package, NusFormat.HashFileName(Hashed())));
        Assert.ThrowsExactly<FileNotFoundException>(() => new NusUnpacker(Reference.CommonKey).Unpack(_package, _output));

        File.Delete(Path.Combine(_package, NusFormat.ContentFileName(1)));
        Assert.ThrowsExactly<FileNotFoundException>(() => new NusUnpacker(Reference.CommonKey).Unpack(_package, _output));

        File.Delete(Path.Combine(_package, NusFormat.TicketFileName));
        Assert.ThrowsExactly<FileNotFoundException>(() => new NusUnpacker(Reference.CommonKey).Unpack(_package, _output));

        File.Delete(Path.Combine(_package, NusFormat.TmdFileName));
        Assert.ThrowsExactly<FileNotFoundException>(() => NusUnpacker.Unpack(_package, _output, Reference.TitleKey));
    }

    [TestMethod]
    public void Unpack_TruncatedContent_ThrowsInvalidDataException()
    {
        var path = Path.Combine(_package, NusFormat.ContentFileName(Hashed()));
        var bytes = File.ReadAllBytes(path);
        File.WriteAllBytes(path, bytes.Take(bytes.Length - 16).ToArray());

        var error = Assert.ThrowsExactly<InvalidDataException>(() => new NusUnpacker(Reference.CommonKey).Unpack(_package, _output));

        StringAssert.Contains(error.Message, "ends before");
    }

    [TestMethod]
    public void Unpack_Cancelled_ThrowsOperationCanceledException()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        Assert.ThrowsExactly<OperationCanceledException>(() => new NusUnpacker(Reference.CommonKey).Unpack(_package, _output, cancellationToken: source.Token));
    }

    [TestMethod]
    public void Unpack_BadArguments_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new NusUnpacker(Reference.CommonKey).Unpack("", _output));
        Assert.ThrowsExactly<ArgumentException>(() => new NusUnpacker(Reference.CommonKey).Unpack(_package, " "));
        Assert.ThrowsExactly<DirectoryNotFoundException>(() => new NusUnpacker(Reference.CommonKey).Unpack(Path.Combine(_root, "nope"), _output));
    }

    [TestMethod]
    public void KeysMatch_RightAndWrongKeys_TellsThemApart()
    {
        Assert.IsTrue(new NusUnpacker(Reference.CommonKey).KeysMatch(_package));
        Assert.IsFalse(new NusUnpacker(WrongKey).KeysMatch(_package));
        Assert.IsTrue(NusUnpacker.KeysMatch(_package, Reference.TitleKey));
        Assert.IsFalse(NusUnpacker.KeysMatch(_package, new TitleKey(WrongKey.ToArray())));
        Assert.ThrowsExactly<ArgumentException>(() => NusUnpacker.KeysMatch("", Reference.TitleKey));
    }

    private uint Hashed() => Record(r => r.IsHashed);

    private uint Plain() => Record(r => !r.IsHashed && r.Index > 0);

    private uint Record(Func<ContentRecord, bool> pick) =>
        Tmd.Parse(File.ReadAllBytes(Path.Combine(_package, NusFormat.TmdFileName))).Contents.First(pick).Id;
}
