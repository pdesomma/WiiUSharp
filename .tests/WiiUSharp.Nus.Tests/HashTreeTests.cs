namespace WiiUSharp.Nus.Tests;

[TestClass]
public class HashTreeTests
{
    [TestMethod]
    public void Compute_EmptyStream_IsOneZeroBlock()
    {
        var tree = HashTree.Compute(new MemoryStream(), CancellationToken.None);

        Assert.AreEqual(1, tree.BlockCount);
        CollectionAssert.AreEqual(Reference.Sha1(new byte[NusFormat.HashBlockDataSize]), tree.BlockHash(0));
    }

    [TestMethod]
    public void Compute_ExactMultiple_HasNoExtraBlock()
    {
        var tree = HashTree.Compute(new MemoryStream(Reference.Pattern(2 * NusFormat.HashBlockDataSize, 1)), CancellationToken.None);

        Assert.AreEqual(2, tree.BlockCount);
    }

    [TestMethod]
    public void Compute_PartialLastBlock_ZeroPadsIt()
    {
        var data = Reference.Pattern(NusFormat.HashBlockDataSize + 100, 2);

        var tree = HashTree.Compute(new MemoryStream(data), CancellationToken.None);

        Assert.AreEqual(2, tree.BlockCount);
        CollectionAssert.AreEqual(Reference.Sha1(Reference.Slice(data, 0, NusFormat.HashBlockDataSize)), tree.BlockHash(0));
        CollectionAssert.AreEqual(Reference.Sha1(Reference.Padded(Reference.Slice(data, NusFormat.HashBlockDataSize, 100), NusFormat.HashBlockDataSize)), tree.BlockHash(1));
    }

    [TestMethod]
    public void HeaderFor_SingleBlock_ChainsH0ThroughH2WithZeroSiblings()
    {
        var data = Reference.Pattern(500, 3);
        var h0 = Reference.Sha1(Reference.Padded(data, NusFormat.HashBlockDataSize));
        var h1 = Reference.Sha1(Group(h0));
        var h2 = Reference.Sha1(Group(h1));
        var h3 = Reference.Sha1(Group(h2));

        var tree = HashTree.Compute(new MemoryStream(data), CancellationToken.None);
        var header = tree.HeaderFor(0);

        Assert.AreEqual(NusFormat.HashBlockHeaderSize, header.Length);
        CollectionAssert.AreEqual(Group(h0), Reference.Slice(header, 0, 320));
        CollectionAssert.AreEqual(Group(h1), Reference.Slice(header, 320, 320));
        CollectionAssert.AreEqual(Group(h2), Reference.Slice(header, 640, 320));
        CollectionAssert.AreEqual(new byte[64], Reference.Slice(header, 960, 64));
        CollectionAssert.AreEqual(h3, tree.H3Bytes());
    }

    [TestMethod]
    public void HeaderFor_SeventeenthBlock_UsesSecondH0GroupAndFirstH1Group()
    {
        var data = Reference.Pattern(17 * NusFormat.HashBlockDataSize, 4);
        var h0 = Enumerable.Range(0, 17).Select(b => Reference.Sha1(Reference.Slice(data, b * NusFormat.HashBlockDataSize, NusFormat.HashBlockDataSize))).ToArray();
        var h1 = new[] { Reference.Sha1(Group(h0.Take(16).ToArray())), Reference.Sha1(Group(h0[16])) };

        var tree = HashTree.Compute(new MemoryStream(data), CancellationToken.None);
        var header = tree.HeaderFor(16);

        CollectionAssert.AreEqual(Group(h0[16]), Reference.Slice(header, 0, 320));
        CollectionAssert.AreEqual(Group(h1), Reference.Slice(header, 320, 320));
        Assert.AreEqual(NusFormat.HashSize, tree.H3Bytes().Length);
    }

    [TestMethod]
    public void HeaderFor_OutOfRange_ThrowsArgumentOutOfRangeException()
    {
        var tree = HashTree.Compute(new MemoryStream(new byte[10]), CancellationToken.None);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => tree.HeaderFor(1));
    }

    private static byte[] Group(params byte[][] hashes)
    {
        var group = new byte[NusFormat.HashesPerGroup * NusFormat.HashSize];
        for (var i = 0; i < hashes.Length; i++)
            hashes[i].CopyTo(group, i * NusFormat.HashSize);
        return group;
    }
}
