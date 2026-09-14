namespace WiiUSharp.Nfs.Tests;

[TestClass]
public class NfsConverterTests
{
    private static readonly NfsKey Key = new(new byte[16]);

    [TestMethod]
    public void FromIsoThenToIsoRestoresStoredDataAndPadsToALayer()
    {
        using var dir = new TempDirectory();
        var content = Path.Combine(dir.Path, "content");
        var temp = Path.Combine(dir.Path, "temp");
        var iso = new byte[12 * NfsFormat.SectorSize];
        for (var i = 0; i < iso.Length; i++)
            iso[i] = (byte)(i * 5);
        var converter = new NfsConverter(new CopyCipher(new DiscDataSpan(10 * NfsFormat.SectorSize, 2 * NfsFormat.SectorSize)));

        var files = converter.FromIso(new MemoryStream(iso), Key, content, temp);
        var output = new CountingStream();
        converter.ToIso(content, Key, output);

        Assert.AreEqual(1, files.Count);
        Assert.AreEqual(0, Directory.GetFiles(temp).Length);
        Assert.AreEqual(NfsConverter.SingleLayerSize, output.Position);
        CollectionAssert.AreEqual(iso.Take(NfsFormat.SectorSize).ToArray(), output.Prefix.Take(NfsFormat.SectorSize).ToArray());
        CollectionAssert.AreEqual(iso.Skip(10 * NfsFormat.SectorSize).ToArray(), output.Prefix.Skip(10 * NfsFormat.SectorSize).Take(2 * NfsFormat.SectorSize).ToArray());
        CollectionAssert.AreEqual(new byte[NfsFormat.SectorSize], output.Prefix.Skip(NfsFormat.SectorSize).Take(NfsFormat.SectorSize).ToArray());
    }

    private sealed class CopyCipher : IPartitionCipher
    {
        private readonly DiscDataSpan _span;

        public CopyCipher(DiscDataSpan span)
        {
            _span = span;
        }

        public DiscDataSpan Decrypt(Stream iso, Stream payload, CancellationToken cancellationToken)
        {
            iso.CopyTo(payload);
            return _span;
        }

        public void Encrypt(Stream payload, Stream iso, CancellationToken cancellationToken) => payload.CopyTo(iso);
    }

    private sealed class CountingStream : Stream
    {
        private readonly MemoryStream _prefix = new();
        private long _position;

        public byte[] Prefix => _prefix.ToArray();
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _position;
        public override long Position { get => _position; set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            var keep = (int)Math.Max(0, Math.Min(count, 16L * NfsFormat.SectorSize - _prefix.Length));
            _prefix.Write(buffer, offset, keep);
            _position += count;
        }
    }
}
