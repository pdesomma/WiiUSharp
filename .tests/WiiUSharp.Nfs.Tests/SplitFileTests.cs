namespace WiiUSharp.Nfs.Tests;

[TestClass]
public class SplitFileTests
{
    private const long SmallFileSize = NfsFormat.HeaderSize + 2 * NfsFormat.SectorSize;

    [TestMethod]
    public void SinkRollsOverAtExactlyTheFileSize()
    {
        using var dir = new TempDirectory();
        var data = Enumerable.Range(0, (int)(NfsFormat.HeaderSize + 5 * NfsFormat.SectorSize)).Select(i => (byte)i).ToArray();

        IReadOnlyList<string> paths;
        using (var sink = new SplitFileSink(dir.Path, SmallFileSize))
        {
            sink.Write(data, 0, NfsFormat.HeaderSize);
            for (var i = 0; i < 5; i++)
                sink.Write(data, NfsFormat.HeaderSize + i * NfsFormat.SectorSize, NfsFormat.SectorSize);
            paths = sink.Paths;
        }

        CollectionAssert.AreEqual(
            new[] { "hif_000000.nfs", "hif_000001.nfs", "hif_000002.nfs" },
            paths.Select(Path.GetFileName).ToArray());
        Assert.AreEqual(SmallFileSize, new FileInfo(paths[0]).Length);
        Assert.AreEqual(SmallFileSize, new FileInfo(paths[1]).Length);
        Assert.AreEqual(NfsFormat.SectorSize - NfsFormat.HeaderSize, new FileInfo(paths[2]).Length);
        CollectionAssert.AreEqual(data, paths.SelectMany(File.ReadAllBytes).ToArray());
    }

    [TestMethod]
    public void SourceReadsAcrossFileBoundariesAndSkipsTheHeader()
    {
        using var dir = new TempDirectory();
        var header = new byte[NfsFormat.HeaderSize];
        var packed = Enumerable.Range(0, 5 * NfsFormat.SectorSize).Select(i => (byte)(i * 7)).ToArray();
        IReadOnlyList<string> paths;
        using (var sink = new SplitFileSink(dir.Path, SmallFileSize))
        {
            sink.Write(header, 0, header.Length);
            sink.Write(packed, 0, packed.Length);
            paths = sink.Paths;
        }

        using var source = new SplitFileSource(paths);

        Assert.AreEqual(packed.Length, source.Length);
        var all = new byte[packed.Length];
        Assert.AreEqual(packed.Length, source.Read(0, all, 0, all.Length));
        CollectionAssert.AreEqual(packed, all);

        var straddle = new byte[64];
        var at = 2 * NfsFormat.SectorSize - 32;
        Assert.AreEqual(64, source.Read(at, straddle, 0, 64));
        CollectionAssert.AreEqual(packed.Skip((int)at).Take(64).ToArray(), straddle);

        Assert.AreEqual(0, source.Read(packed.Length, straddle, 0, 64));
    }
}
