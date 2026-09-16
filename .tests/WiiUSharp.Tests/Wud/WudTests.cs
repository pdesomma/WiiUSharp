using WiiUSharp.Nus;
using WiiUSharp.Nus.Tests;

namespace WiiUSharp.Wud.Tests;

[TestClass]
public class WudTests
{
    private string _root = null!;

    [TestInitialize]
    public void Initialize()
    {
        _root = Reference.TempRoot();
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, recursive: true);

    [TestMethod]
    public void Read_FakeDisc_ListsPartitionsAndTheGameTitle()
    {
        var (bytes, _) = FakeWud.Build(_root);

        var disc = WudDisc.Read(new MemoryStream(bytes), FakeWud.DiscKey);

        Assert.AreEqual(FakeWud.ProductCode, disc.ProductCode);
        CollectionAssert.AreEqual(new[] { "SI", "GM000500021ABCDE0000" }, disc.Partitions.Select(p => p.Name).ToArray());
        Assert.AreEqual(FakeWud.SystemPartitionOffset, disc.Partitions[0].Offset);
        Assert.AreEqual(FakeWud.GamePartitionOffset, disc.Partitions[1].Offset);
        Assert.IsFalse(disc.Partitions[0].IsGame);
        Assert.IsTrue(disc.Partitions[1].IsGame);
        Assert.AreEqual(1, disc.Titles.Count);
        var title = disc.Titles[0];
        Assert.AreEqual(Reference.TitleId, title.TitleId);
        Assert.AreSame(disc.Partitions[1], title.Partition);
        Assert.IsTrue(title.HasCertificateChain);
        Assert.AreEqual(9, title.Tmd.Contents.Count);
        Assert.AreEqual(title.Tmd.Contents.Sum(c => c.EncryptedSize), title.Size);
        Assert.AreEqual(2, title.Partition.H3Count);
        Assert.AreEqual(1, title.Partition.FstHashType);
        Assert.AreEqual(2, title.Partition.FstEncryptionType);
    }

    [TestMethod]
    public void Read_NoCertificateOnDisc_StillFindsTheTitle()
    {
        var (bytes, _) = FakeWud.Build(_root, withCertificates: false);

        var disc = WudDisc.Read(new MemoryStream(bytes), FakeWud.DiscKey);

        Assert.AreEqual(1, disc.Titles.Count);
        Assert.IsFalse(disc.Titles[0].HasCertificateChain);
    }

    [TestMethod]
    public void Read_WrongKeyOrNotADisc_ThrowsInvalidDataException()
    {
        var (bytes, _) = FakeWud.Build(_root);
        var wrongKey = new DiscKey(new byte[16]);

        var error = Assert.ThrowsExactly<InvalidDataException>(() => WudDisc.Read(new MemoryStream(bytes), wrongKey));
        StringAssert.Contains(error.Message, "disc key");
        Assert.ThrowsExactly<InvalidDataException>(() => WudDisc.Read(new MemoryStream(new byte[0x20000]), FakeWud.DiscKey));
        Assert.ThrowsExactly<ArgumentNullException>(() => WudDisc.Read(null!, FakeWud.DiscKey));
    }

    [TestMethod]
    public void WritePackage_FakeDisc_ReproducesThePackerOutputByteForByte()
    {
        var (bytes, package) = FakeWud.Build(_root);
        var disc = WudDisc.Read(new MemoryStream(bytes), FakeWud.DiscKey);
        var output = Path.Combine(_root, "installable");
        var reports = new List<string>();

        disc.Titles[0].WritePackage(disc.Image, Reference.CommonKey, output, new SyncProgress<string>(reports.Add));

        var expected = Directory.GetFiles(package).Select(Path.GetFileName).OrderBy(f => f).ToArray();
        CollectionAssert.AreEqual(expected, Directory.GetFiles(output).Select(Path.GetFileName).OrderBy(f => f).ToArray());
        foreach (var name in expected)
            CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(package, name!)), File.ReadAllBytes(Path.Combine(output, name!)), name);
        Assert.AreEqual(9, reports.Count);

        var unpacked = Path.Combine(_root, "unpacked");
        new NusUnpacker(Reference.CommonKey).Unpack(output, unpacked);
        foreach (var pair in FakeTitle.Files)
            CollectionAssert.AreEqual(pair.Value, File.ReadAllBytes(Path.Combine(unpacked, pair.Key.Replace('/', Path.DirectorySeparatorChar))), pair.Key);
    }

    [TestMethod]
    public void WritePackage_NoH3WordsAndNoCertificates_StillMatchesAndStubsTheChain()
    {
        var (bytes, package) = FakeWud.Build(_root, h3Words: 0, withCertificates: false);
        var disc = WudDisc.Read(new MemoryStream(bytes), FakeWud.DiscKey);
        var output = Path.Combine(_root, "installable");

        disc.Titles[0].WritePackage(disc.Image, Reference.CommonKey, output);

        foreach (var record in disc.Titles[0].Tmd.Contents.Where(c => c.IsHashed))
            CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(package, NusFormat.HashFileName(record.Id))), File.ReadAllBytes(Path.Combine(output, NusFormat.HashFileName(record.Id))));
        CollectionAssert.AreEqual(CertificateChain.Stub(), File.ReadAllBytes(Path.Combine(output, NusFormat.CertificateFileName)));
    }

    [TestMethod]
    public void WritePackage_WrongCommonKey_ThrowsBeforeWriting()
    {
        var (bytes, _) = FakeWud.Build(_root);
        var disc = WudDisc.Read(new MemoryStream(bytes), FakeWud.DiscKey);
        var output = Path.Combine(_root, "installable");

        var error = Assert.ThrowsExactly<InvalidDataException>(() => disc.Titles[0].WritePackage(disc.Image, new CommonKey(new byte[16]), output));

        StringAssert.Contains(error.Message, "FST");
        Assert.IsFalse(Directory.Exists(output));
        Assert.ThrowsExactly<ArgumentNullException>(() => disc.Titles[0].WritePackage(null!, Reference.CommonKey, output));
        Assert.ThrowsExactly<ArgumentNullException>(() => disc.Titles[0].WritePackage(disc.Image, Reference.CommonKey, null!));
    }

    [TestMethod]
    public void WritePackage_DamagedH3Table_ThrowsInvalidDataException()
    {
        var (bytes, _) = FakeWud.Build(_root);
        bytes[FakeWud.GamePartitionOffset + WudFormat.PartitionHeaderFixedSize + 2 * 4 + 3] ^= 0xFF;
        var disc = WudDisc.Read(new MemoryStream(bytes), FakeWud.DiscKey);

        var error = Assert.ThrowsExactly<InvalidDataException>(() => disc.Titles[0].WritePackage(disc.Image, Reference.CommonKey, Path.Combine(_root, "installable")));

        StringAssert.Contains(error.Message, "H3");
    }

    [TestMethod]
    public void WuxStream_Open_ReadsBackTheWholeDisc()
    {
        var (bytes, _) = FakeWud.Build(_root);
        var wux = FakeWud.Wux(bytes, sectorSize: 0x2000);
        Assert.IsTrue(wux.Length < bytes.Length, "empty sectors are stored once");

        using var stream = WuxStream.Open(new MemoryStream(wux));

        Assert.AreEqual(bytes.Length, stream.Length);
        Assert.AreEqual(0x2000, stream.SectorSize);
        var all = new byte[bytes.Length];
        var read = 0;
        while (read < all.Length)
        {
            var n = stream.Read(all, read, Math.Min(0x3001, all.Length - read));
            Assert.IsTrue(n > 0);
            read += n;
        }
        CollectionAssert.AreEqual(bytes, all);

        stream.Seek(-16, SeekOrigin.End);
        var tail = new byte[32];
        Assert.AreEqual(16, stream.Read(tail, 0, tail.Length), "reads stop at the end");
        Assert.IsTrue(WuxStream.IsWux(new MemoryStream(wux)));
        Assert.IsFalse(WuxStream.IsWux(new MemoryStream(bytes)));
    }

    [TestMethod]
    public void WuxStream_Open_RejectsBadInput()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => WuxStream.Open(null!));
        Assert.ThrowsExactly<InvalidDataException>(() => WuxStream.Open(new MemoryStream(new byte[64])));
        var (bytes, _) = FakeWud.Build(_root);
        var wux = FakeWud.Wux(bytes);
        Assert.ThrowsExactly<InvalidDataException>(() => WuxStream.Open(new MemoryStream(wux.Take(40).ToArray())), "index cut short");
        Assert.ThrowsExactly<ArgumentNullException>(() => WuxStream.IsWux(null!));
    }

    [TestMethod]
    public void WudDisc_Read_WorksThroughAWuxStream()
    {
        var (bytes, _) = FakeWud.Build(_root);
        using var stream = WuxStream.Open(new MemoryStream(FakeWud.Wux(bytes)));

        var disc = WudDisc.Read(stream, FakeWud.DiscKey);

        Assert.AreEqual(1, disc.Titles.Count);
    }

    [TestMethod]
    public void WudImage_Open_HandlesWudWuxAndSplitParts()
    {
        var (bytes, _) = FakeWud.Build(_root);
        var wud = Path.Combine(_root, "game.wud");
        File.WriteAllBytes(wud, bytes);
        var wux = Path.Combine(_root, "game.wux");
        File.WriteAllBytes(wux, FakeWud.Wux(bytes));
        var renamed = Path.Combine(_root, "renamed.wud");
        File.WriteAllBytes(renamed, FakeWud.Wux(bytes));
        var half = bytes.Length / 2;
        File.WriteAllBytes(Path.Combine(_root, "game_part1.wud"), bytes.Take(half).ToArray());
        File.WriteAllBytes(Path.Combine(_root, "game_part2.wud"), bytes.Skip(half).ToArray());

        foreach (var path in new[] { wud, wux, renamed, Path.Combine(_root, "game_part1.wud"), Path.Combine(_root, "game_part2.wud") })
        {
            using var image = WudImage.Open(path);
            Assert.AreEqual(bytes.Length, image.Length, path);
            Assert.AreEqual(1, WudDisc.Read(image, FakeWud.DiscKey).Titles.Count, path);
        }
        CollectionAssert.AreEqual(new[] { Path.Combine(_root, "game_part1.wud"), Path.Combine(_root, "game_part2.wud") }, WudImage.Parts(Path.Combine(_root, "game_part2.wud")).ToArray());
        CollectionAssert.AreEqual(new[] { wud }, WudImage.Parts(wud).ToArray());
        Assert.ThrowsExactly<NotSupportedException>(() => WudImage.Open(Path.Combine(_root, "game.iso")));
        Assert.ThrowsExactly<ArgumentNullException>(() => WudImage.Open(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => WudImage.Parts(null!));
    }

    [TestMethod]
    public void DiscKey_ParseFileAndBeside_AgreeAndPrintHex()
    {
        var hex = "00112233445566778899aabbccddeeff";
        var parsed = DiscKey.Parse(hex);
        var file = Path.Combine(_root, WudFormat.DiscKeyFileName);
        File.WriteAllBytes(file, parsed.ToArray());

        Assert.AreEqual(parsed, DiscKey.FromFile(file));
        Assert.AreEqual(parsed, DiscKey.Beside(Path.Combine(_root, "game.wux")));
        Assert.IsNull(DiscKey.Beside(Path.Combine(_root, "elsewhere", "game.wux")));
        Assert.AreEqual(hex, parsed.ToString());
        Assert.AreNotEqual(parsed, new DiscKey(new byte[16]));
        Assert.AreEqual(parsed.GetHashCode(), DiscKey.Parse(hex).GetHashCode());
        Assert.ThrowsExactly<ArgumentException>(() => new DiscKey(new byte[15]));
        Assert.ThrowsExactly<ArgumentNullException>(() => new DiscKey(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => DiscKey.Beside(null!));
    }

    [TestMethod]
    public void WudPartition_Constructor_RejectsBadHeaders()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new WudPartition(0, null!, 0, new byte[0x40]));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WudPartition(0, "SI", 0, null!));
        Assert.ThrowsExactly<ArgumentException>(() => new WudPartition(0, "SI", 0, new byte[0x10]));
        Assert.ThrowsExactly<InvalidDataException>(() => new WudPartition(0, "SI", 0, new byte[0x40]), "no magic");
        Assert.ThrowsExactly<ArgumentNullException>(() => new WudTitle(null!, new byte[1], new byte[1], null));
    }
}
