namespace WiiUSharp.Nfs.Tests;

[TestClass]
public class RoundTripTests
{
    private const int GameSectors = 5;
    private const int GameStartSector = 10;
    private const int PayloadSectors = GameStartSector + GameSectors;
    private static readonly NfsKey Key = new(Enumerable.Range(0x10, 16).Select(i => (byte)i).ToArray());

    [TestMethod]
    public void OpenFailsWithoutFilesOrHeader()
    {
        using var dir = new TempDirectory();

        Assert.ThrowsExactly<FileNotFoundException>(() => NfsReader.Open(dir.Path, Key));

        File.WriteAllBytes(Path.Combine(dir.Path, "hif_000000.nfs"), new byte[0x300]);
        Assert.ThrowsExactly<FormatException>(() => NfsReader.Open(dir.Path, Key));
    }

    [TestMethod]
    public void PayloadStreamSeeksAndReadsAcrossGapsAndSectors()
    {
        using var dir = new TempDirectory();
        var payload = Payload();
        new NfsWriter(Key).Write(new MemoryStream(payload), Span(), dir.Path);

        using var stream = NfsReader.Open(dir.Path, Key).OpenPayload();

        var at = 1 * NfsFormat.SectorSize - 100;
        stream.Seek(at, SeekOrigin.Begin);
        var buffer = new byte[200];
        Assert.AreEqual(200, stream.Read(buffer, 0, 200));
        CollectionAssert.AreEqual(payload.Skip((int)at).Take(100).ToArray(), buffer.Take(100).ToArray());
        CollectionAssert.AreEqual(new byte[100], buffer.Skip(100).ToArray());

        at = 8 * NfsFormat.SectorSize - 50;
        stream.Position = at;
        Assert.AreEqual(200, stream.Read(buffer, 0, 200));
        CollectionAssert.AreEqual(new byte[50], buffer.Take(50).ToArray());
        CollectionAssert.AreEqual(payload.Skip((int)at + 50).Take(150).ToArray(), buffer.Skip(50).ToArray());

        stream.Seek(-10, SeekOrigin.End);
        Assert.AreEqual(10, stream.Read(buffer, 0, 200));
        Assert.AreEqual(0, stream.Read(buffer, 0, 200));
    }

    [TestMethod]
    public void TruncatedContainerIsRejectedOnOpen()
    {
        using var dir = new TempDirectory();
        var files = new NfsWriter(Key).Write(new MemoryStream(Payload()), Span(), dir.Path);
        using (var file = new FileStream(files[0], FileMode.Open))
            file.SetLength(file.Length - NfsFormat.SectorSize);

        var reader = NfsReader.Open(dir.Path, Key);

        Assert.ThrowsExactly<FormatException>(() => reader.OpenPayload());
    }

    [TestMethod]
    public void WriteProducesHeaderAndPackedSectorsOnly()
    {
        using var dir = new TempDirectory();
        var reports = new List<long>();

        var files = new NfsWriter(Key).Write(new MemoryStream(Payload()), Span(), dir.Path, new Progress(reports));

        Assert.AreEqual(1, files.Count);
        Assert.AreEqual(NfsFormat.HeaderSize + 8L * NfsFormat.SectorSize, new FileInfo(files[0]).Length);
        Assert.AreEqual(8L * NfsFormat.SectorSize, reports.Last());

        var header = NfsReader.Open(dir.Path, Key).Header;
        CollectionAssert.AreEqual(
            new[] { new NfsPart(0, 1), new NfsPart(8, 2), new NfsPart(GameStartSector, GameSectors) },
            header.Parts.ToArray());
    }

    [TestMethod]
    public void WriteReplacesStaleFiles()
    {
        using var dir = new TempDirectory();
        File.WriteAllBytes(Path.Combine(dir.Path, "hif_000007.nfs"), new byte[10]);

        new NfsWriter(Key).Write(new MemoryStream(Payload()), Span(), dir.Path);

        Assert.IsFalse(File.Exists(Path.Combine(dir.Path, "hif_000007.nfs")));
    }

    [TestMethod]
    public void WriteThenReadRestoresStoredSectorsAndZeroesGaps()
    {
        using var dir = new TempDirectory();
        var payload = Payload();

        new NfsWriter(Key).Write(new MemoryStream(payload), Span(), dir.Path);
        using var stream = NfsReader.Open(dir.Path, Key).OpenPayload();

        Assert.AreEqual(payload.Length, stream.Length);
        var actual = new byte[payload.Length];
        var read = 0;
        while (read < actual.Length)
        {
            var n = stream.Read(actual, read, actual.Length - read);
            if (n == 0)
                break;
            read += n;
        }
        Assert.AreEqual(payload.Length, read);

        for (var s = 0; s < PayloadSectors; s++)
        {
            var stored = s == 0 || s == 8 || s == 9 || s >= GameStartSector;
            var expected = stored ? Sector(payload, s) : new byte[NfsFormat.SectorSize];
            CollectionAssert.AreEqual(expected, Sector(actual, s), $"sector {s}");
        }
    }

    [TestMethod]
    public void WriteWorksFromAForwardOnlyStream()
    {
        using var dir = new TempDirectory();
        var payload = Payload();

        new NfsWriter(Key).Write(new ForwardOnlyStream(payload), Span(), dir.Path);
        using var stream = NfsReader.Open(dir.Path, Key).OpenPayload();

        stream.Position = GameStartSector * NfsFormat.SectorSize;
        var buffer = new byte[NfsFormat.SectorSize];
        Assert.AreEqual(buffer.Length, stream.Read(buffer, 0, buffer.Length));
        CollectionAssert.AreEqual(Sector(payload, GameStartSector), buffer);
    }

    private static byte[] Payload()
    {
        var bytes = new byte[PayloadSectors * NfsFormat.SectorSize];
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = (byte)(i / NfsFormat.SectorSize * 17 + i * 3);
        return bytes;
    }

    private static byte[] Sector(byte[] bytes, int index) =>
        bytes.Skip(index * NfsFormat.SectorSize).Take(NfsFormat.SectorSize).ToArray();

    private static DiscDataSpan Span() =>
        new(GameStartSector * NfsFormat.SectorSize, GameSectors * NfsFormat.SectorSize);

    private sealed class ForwardOnlyStream : MemoryStream
    {
        public ForwardOnlyStream(byte[] bytes) : base(bytes, writable: false)
        {
        }

        public override bool CanSeek => false;
    }

    private sealed class Progress : IProgress<long>
    {
        private readonly List<long> _reports;

        public Progress(List<long> reports)
        {
            _reports = reports;
        }

        public void Report(long value) => _reports.Add(value);
    }
}
