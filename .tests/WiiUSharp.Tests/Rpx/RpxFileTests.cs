using System.IO.Compression;
using System.Text;

namespace WiiUSharp.Rpx.Tests;

[TestClass]
public class RpxFileTests
{
    [TestMethod]
    public void Crc32_KnownAnswer_MatchesZlib()
    {
        Assert.AreEqual(0xCBF43926u, Crc32.Compute(Encoding.ASCII.GetBytes("123456789")));
        Assert.AreEqual(0xCBF43926u, Crc32.Update(Crc32.Update(0, Encoding.ASCII.GetBytes("12345"), 0, 5), Encoding.ASCII.GetBytes("6789"), 0, 4));
        Assert.AreEqual(0u, Crc32.Compute(Array.Empty<byte>()));
    }

    [TestMethod]
    public void Parse_NotElf_ThrowsInvalidDataException()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => RpxFile.Parse(new byte[0x100]));
    }

    [TestMethod]
    public void Parse_WrongElfType_ThrowsInvalidDataException()
    {
        var bytes = FakeRpx.Build();
        bytes[0x10] = 0;
        bytes[0x11] = 2;

        Assert.ThrowsExactly<InvalidDataException>(() => RpxFile.Parse(bytes));
    }

    [TestMethod]
    public void Parse_SectionOutsideFile_ThrowsInvalidDataException()
    {
        var bytes = FakeRpx.Build();
        var textHeader = (int)FakeRpx.SectionHeadersOffset + RpxFormat.SectionHeaderSize;
        bytes[textHeader + 0x14] = 0x7F;

        Assert.ThrowsExactly<InvalidDataException>(() => RpxFile.Parse(bytes));
    }

    [TestMethod]
    public void Parse_PlainFile_ReadsHeaderSectionsAndNames()
    {
        var rpx = RpxFile.Parse(FakeRpx.Build());

        Assert.AreEqual(RpxFormat.RplType, rpx.Header.Type);
        Assert.AreEqual(0x14, rpx.Header.Machine);
        Assert.AreEqual(0x02000000u, rpx.Header.Entry);
        Assert.AreEqual(7, rpx.Sections.Count);
        CollectionAssert.AreEqual(FakeRpx.Sections.Select(s => s.Name).ToArray(), rpx.Sections.Select(rpx.NameOf).ToArray());
        CollectionAssert.AreEqual(FakeRpx.Text, rpx.Sections[1].Data);
        CollectionAssert.AreEqual(FakeRpx.Rodata, rpx.Sections[2].Data);
        Assert.AreEqual(0, rpx.Sections[3].Data.Length);
        Assert.AreEqual((uint)FakeRpx.BssSize, rpx.Sections[3].StoredSize);
        Assert.IsFalse(rpx.Sections[0].HasData);
        Assert.IsTrue(rpx.Sections[1].HasData);
        Assert.IsFalse(rpx.Sections[1].StoredCompressed);
        Assert.AreEqual(0x6u, rpx.Sections[1].Flags);
        Assert.AreEqual(0x02010000u, rpx.Sections[1].Address);
        Assert.AreSame(rpx.Sections[2], rpx.FindSection(".rodata"));
        Assert.IsNull(rpx.FindSection(".nope"));
    }

    [TestMethod]
    public void Save_Uncompressed_LaysOutSectionsAlignedWithFreshCrcTable()
    {
        var rpx = RpxFile.Parse(FakeRpx.Build());
        var output = new MemoryStream();

        rpx.Save(output, compress: false);
        var bytes = output.ToArray();

        CollectionAssert.AreEqual(rpx.Header.ToBytes(), bytes.Take(RpxFormat.HeaderSize).ToArray());
        var reread = RpxFile.Parse(bytes);
        var position = (uint)(FakeRpx.SectionHeadersOffset + 7 * RpxFormat.SectionHeaderSize);
        foreach (var index in new[] { 1, 2, 4, 5, 6 })
        {
            var section = reread.Sections[index];
            Assert.AreEqual(position, section.StoredOffset, $"section {index} offset");
            Assert.IsFalse(section.StoredCompressed);
            if (index != 5)
                CollectionAssert.AreEqual(rpx.Sections[index].Data, section.Data, $"section {index} data");
            position = (position + section.StoredSize + 0x3F) & ~0x3Fu;
        }
        Assert.AreEqual(0u, reread.Sections[0].StoredOffset);
        Assert.AreEqual((uint)FakeRpx.BssSize, reread.Sections[3].StoredSize);
        Assert.AreEqual(0, reread.Sections[3].Data.Length);

        var crcs = Enumerable.Range(0, 7).Select(i => ReadUInt32(reread.Sections[5].Data, i * 4)).ToArray();
        CollectionAssert.AreEqual(new[]
        {
            0u,
            Crc32.Compute(FakeRpx.Text),
            Crc32.Compute(FakeRpx.Rodata),
            0u,
            Crc32.Compute(FakeRpx.Names),
            0u,
            Crc32.Compute(FakeRpx.FileInfo),
        }, crcs);
    }

    [TestMethod]
    public void Save_Compressed_DeflatesBigSectionsAndLeavesTheRest()
    {
        var rpx = RpxFile.Parse(FakeRpx.Build());
        var output = new MemoryStream();

        rpx.Save(output, compress: true);
        var bytes = output.ToArray();
        var reread = RpxFile.Parse(bytes);

        Assert.IsTrue(reread.Sections[1].StoredCompressed, ".text");
        Assert.IsFalse(reread.Sections[2].StoredCompressed, "small incompressible .rodata");
        Assert.IsFalse(reread.Sections[5].StoredCompressed, "crcs");
        Assert.IsFalse(reread.Sections[6].StoredCompressed, "fileinfo");
        foreach (var i in new[] { 1, 2, 3, 4, 6 })
            CollectionAssert.AreEqual(rpx.Sections[i].Data, reread.Sections[i].Data, $"section {i}");
        Assert.AreEqual(0x6u, reread.Sections[1].Flags, "zlib bit is not part of Flags");
        Assert.IsTrue(reread.Sections[1].StoredSize < FakeRpx.Text.Length);

        var text = reread.Sections[1];
        var stored = bytes.Skip((int)text.StoredOffset).Take((int)text.StoredSize).ToArray();
        Assert.AreEqual((uint)FakeRpx.Text.Length, ReadUInt32(stored, 0), "plain length prefix");
        Assert.AreEqual(0x78, stored[4], "zlib header");
        using var inflate = new DeflateStream(new MemoryStream(stored, 6, stored.Length - 10), CompressionMode.Decompress);
        var inflated = new MemoryStream();
        inflate.CopyTo(inflated);
        CollectionAssert.AreEqual(FakeRpx.Text, inflated.ToArray());
        Assert.AreEqual(Adler32(FakeRpx.Text), ReadUInt32(stored, stored.Length - 4), "adler trailer");

        var crcs = Enumerable.Range(0, 7).Select(i => ReadUInt32(reread.Sections[5].Data, i * 4)).ToArray();
        Assert.AreEqual(Crc32.Compute(FakeRpx.Text), crcs[1], "crc is over plain data");
    }

    [TestMethod]
    public void Save_CompressedThenParsedThenSavedPlain_RoundTripsSectionData()
    {
        var rpx = RpxFile.Parse(FakeRpx.Build());
        var compressed = new MemoryStream();
        rpx.Save(compressed, compress: true);
        var plain = new MemoryStream();

        RpxFile.Parse(compressed.ToArray()).Save(plain, compress: false);
        var reread = RpxFile.Parse(plain.ToArray());

        foreach (var i in new[] { 1, 2, 3, 4, 6 })
            CollectionAssert.AreEqual(rpx.Sections[i].Data, reread.Sections[i].Data, $"section {i}");
        Assert.IsTrue(reread.Sections.All(s => !s.StoredCompressed));
        CollectionAssert.AreEqual(RpxFile.Parse(compressed.ToArray()).Sections[5].Data, reread.Sections[5].Data, "crc table is stable");
    }

    [TestMethod]
    public void Data_Replaced_SavesNewBytesAndCrc()
    {
        var rpx = RpxFile.Parse(FakeRpx.Build());
        var replacement = Encoding.ASCII.GetBytes("WUP-JAAE\0\0\0\0new rodata, different length!");
        rpx.Sections[2].Data = replacement;
        var output = new MemoryStream();

        rpx.Save(output, compress: false);
        var reread = RpxFile.Parse(output.ToArray());

        CollectionAssert.AreEqual(replacement, reread.Sections[2].Data);
        Assert.AreEqual(Crc32.Compute(replacement), ReadUInt32(reread.Sections[5].Data, 2 * 4));
        CollectionAssert.AreEqual(FakeRpx.Text, reread.Sections[1].Data, "later sections shift and survive");
    }

    [TestMethod]
    public void Load_FromPath_ReadsTheFile()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".rpx");
        File.WriteAllBytes(path, FakeRpx.Build());
        try
        {
            var rpx = RpxFile.Load(path);
            Assert.AreEqual(7, rpx.Sections.Count);

            rpx.Save(path, compress: true);
            Assert.IsTrue(RpxFile.Load(path).Sections[1].StoredCompressed);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static uint Adler32(byte[] data)
    {
        uint a = 1, b = 0;
        foreach (var value in data)
        {
            a = (a + value) % 65521;
            b = (b + a) % 65521;
        }
        return b << 16 | a;
    }

    private static uint ReadUInt32(byte[] bytes, int offset) =>
        (uint)(bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3]);
}
