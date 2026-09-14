namespace WiiUSharp.Nfs.Tests;

[TestClass]
public class NfsHeaderTests
{
    [TestMethod]
    public void ForDiscProducesTheToolsThreePartLayout()
    {
        var header = NfsHeader.ForDisc(new DiscDataSpan(0xF800000, 0x12345678 * 0x8000L));

        CollectionAssert.AreEqual(
            new[] { new NfsPart(0, 1), new NfsPart(8, 2), new NfsPart(0xF800000 / 0x8000, 0x12345678) },
            header.Parts.ToArray());
    }

    [TestMethod]
    public void ForDiscRejectsUnalignedOrEmptyData()
    {
        Assert.ThrowsExactly<ArgumentException>(() => NfsHeader.ForDisc(new DiscDataSpan(0xF800001, 0x8000)));
        Assert.ThrowsExactly<ArgumentException>(() => NfsHeader.ForDisc(new DiscDataSpan(0xF800000, 0)));
        Assert.ThrowsExactly<ArgumentException>(() => NfsHeader.ForDisc(new DiscDataSpan(0x40000, 0x8000)));
    }

    [TestMethod]
    public void ForDiscRoundsLengthUpToWholeSectors()
    {
        var header = NfsHeader.ForDisc(new DiscDataSpan(0xF800000, 0x8001));

        Assert.AreEqual(2u, header.Parts[2].SectorCount);
    }

    [TestMethod]
    public void LengthsFollowTheParts()
    {
        var header = new NfsHeader(new[] { new NfsPart(0, 1), new NfsPart(8, 2), new NfsPart(10, 5) });

        Assert.AreEqual(15 * 0x8000L, header.PayloadLength);
        Assert.AreEqual(8 * 0x8000L, header.PackedLength);
    }

    [TestMethod]
    public void RejectsOverlappingOrUnorderedParts()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new NfsHeader(new[] { new NfsPart(8, 2), new NfsPart(0, 1) }));
        Assert.ThrowsExactly<ArgumentException>(() => new NfsHeader(new[] { new NfsPart(0, 2), new NfsPart(1, 1) }));
    }

    [TestMethod]
    public void ToBytesMatchesNfs2Iso2NfsByteForByte()
    {
        long gameOffset = 0xF800000, gameLength = 0x1234 * 0x8000L;

        var actual = NfsHeader.ForDisc(new DiscDataSpan(gameOffset, gameLength)).ToBytes();

        CollectionAssert.AreEqual(ToolHeader(gameOffset, gameLength), actual);
    }

    [TestMethod]
    public void TryParseReadsBackWhatToBytesWrote()
    {
        var original = new NfsHeader(new[] { new NfsPart(0, 1), new NfsPart(8, 2), new NfsPart(0x1F0, 0x3E80) });

        Assert.IsTrue(NfsHeader.TryParse(original.ToBytes(), out var parsed));
        CollectionAssert.AreEqual(original.Parts.ToArray(), parsed!.Parts.ToArray());
    }

    [TestMethod]
    public void TryParseRejectsBadInput()
    {
        var good = NfsHeader.ForDisc(new DiscDataSpan(0xF800000, 0x8000)).ToBytes();

        var badMagic = (byte[])good.Clone();
        badMagic[0] = 0x00;
        var badTrailer = (byte[])good.Clone();
        badTrailer[0x1FF] = 0x00;
        var tooManyParts = (byte[])good.Clone();
        tooManyParts[0x13] = 0xFF;
        var overlapping = (byte[])good.Clone();
        overlapping[0x26] = 0x00;
        overlapping[0x27] = 0x05;

        Assert.IsFalse(NfsHeader.TryParse(null, out _));
        Assert.IsFalse(NfsHeader.TryParse(new byte[0x100], out _));
        Assert.IsFalse(NfsHeader.TryParse(badMagic, out _));
        Assert.IsFalse(NfsHeader.TryParse(badTrailer, out _));
        Assert.IsFalse(NfsHeader.TryParse(tooManyParts, out _));
        Assert.IsFalse(NfsHeader.TryParse(overlapping, out _));
        Assert.ThrowsExactly<FormatException>(() => NfsHeader.Parse(badMagic));
    }

    private static byte[] ToolHeader(long gameOffset, long gameLength)
    {
        var header = new byte[0x200];
        for (var i = 0; i < 0x200; i++)
            header[i] = 0xff;

        header[0x0] = 0x45; header[0x1] = 0x47; header[0x2] = 0x47; header[0x3] = 0x53;
        header[0x4] = 0x00; header[0x5] = 0x01; header[0x6] = 0x10; header[0x7] = 0x11;
        for (var i = 0x8; i < 0x13; i++)
            header[i] = 0x00;
        header[0x13] = 0x03;
        header[0x14] = 0x00; header[0x15] = 0x00; header[0x16] = 0x00; header[0x17] = 0x00;
        header[0x18] = 0x00; header[0x19] = 0x00; header[0x1A] = 0x00; header[0x1B] = 0x01;
        header[0x1C] = 0x00; header[0x1D] = 0x00; header[0x1E] = 0x00; header[0x1F] = 0x08;
        header[0x20] = 0x00; header[0x21] = 0x00; header[0x22] = 0x00; header[0x23] = 0x02;
        header[0x24] = (byte)((gameOffset / 0x8000) / 0x1000000);
        header[0x25] = (byte)(((gameOffset / 0x8000) / 0x10000) % 0x100);
        header[0x26] = (byte)(((gameOffset / 0x8000) / 0x100) % 0x10000);
        header[0x27] = (byte)((gameOffset / 0x8000) % 0x1000000);
        header[0x28] = (byte)((gameLength / 0x8000) / 0x1000000);
        header[0x29] = (byte)(((gameLength / 0x8000) / 0x10000) % 0x100);
        header[0x2A] = (byte)(((gameLength / 0x8000) / 0x100) % 0x10000);
        header[0x2B] = (byte)((gameLength / 0x8000) % 0x1000000);
        header[0x1FC] = 0x53; header[0x1FD] = 0x47; header[0x1FE] = 0x47; header[0x1FF] = 0x45;
        return header;
    }
}
