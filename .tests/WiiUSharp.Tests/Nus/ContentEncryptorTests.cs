namespace WiiUSharp.Nus.Tests;

[TestClass]
public class ContentEncryptorTests
{
    private const ContentType Hashed = ContentType.Content | ContentType.Encrypted | ContentType.Hashed;
    private const ContentType Plain = ContentType.Content | ContentType.Encrypted;

    [TestMethod]
    public void EncryptHashed_TwoBlocks_DecryptsBlockByBlockAndHashesH3()
    {
        var data = Reference.Pattern(NusFormat.HashBlockDataSize + 777, 5);
        var output = new MemoryStream();
        var h3 = new MemoryStream();
        using var encryptor = new ContentEncryptor(Reference.TitleKey);

        var record = encryptor.EncryptHashed(3, () => new MemoryStream(data), output, h3, Hashed, CancellationToken.None);
        var tree = HashTree.Compute(new MemoryStream(data), CancellationToken.None);

        Assert.AreEqual(3, record.Index);
        Assert.AreEqual(Hashed, record.Type);
        Assert.AreEqual(2L * NusFormat.HashBlockSize, record.Size);
        Assert.AreEqual(record.Size, output.Length);
        CollectionAssert.AreEqual(tree.H3Bytes(), h3.ToArray());
        CollectionAssert.AreEqual(Reference.Sha1(h3.ToArray()), record.Hash);

        var encrypted = output.ToArray();
        for (var block = 0; block < 2; block++)
        {
            var header = Reference.AesCbc(Reference.TitleKey.ToArray(), Reference.IndexIv(3), Reference.Slice(encrypted, block * NusFormat.HashBlockSize, NusFormat.HashBlockHeaderSize), encrypt: false);
            header[1] ^= 3;
            CollectionAssert.AreEqual(tree.HeaderFor(block), header, $"header {block}");

            var iv = Reference.Slice(tree.BlockHash(block), 0, 16);
            var plain = Reference.AesCbc(Reference.TitleKey.ToArray(), iv, Reference.Slice(encrypted, block * NusFormat.HashBlockSize + NusFormat.HashBlockHeaderSize, NusFormat.HashBlockDataSize), encrypt: false);
            var expected = Reference.Padded(data.Skip(block * NusFormat.HashBlockDataSize).Take(NusFormat.HashBlockDataSize).ToArray(), NusFormat.HashBlockDataSize);
            CollectionAssert.AreEqual(expected, plain, $"data {block}");
        }
    }

    [TestMethod]
    public void EncryptPlain_EmptyStream_WritesOneZeroBlock()
    {
        var output = new MemoryStream();
        using var encryptor = new ContentEncryptor(Reference.TitleKey);

        var record = encryptor.EncryptPlain(1, new MemoryStream(), output, Plain, CancellationToken.None);

        Assert.AreEqual(NusFormat.ContentPadding, record.Size);
        Assert.AreEqual(NusFormat.ContentPadding, output.Length);
        CollectionAssert.AreEqual(Reference.Sha1(new byte[NusFormat.ContentPadding]), record.Hash);
    }

    [TestMethod]
    public void EncryptPlain_OneAndAHalfBlocks_IsOneCbcChainOverPaddedData()
    {
        var data = Reference.Pattern(NusFormat.ContentPadding + 1234, 6);
        var padded = Reference.Padded(data, NusFormat.ContentPadding);
        var output = new MemoryStream();
        using var encryptor = new ContentEncryptor(Reference.TitleKey);

        var record = encryptor.EncryptPlain(2, new MemoryStream(data), output, Plain, CancellationToken.None);

        Assert.AreEqual(padded.Length, record.Size);
        CollectionAssert.AreEqual(Reference.Sha1(padded), record.Hash);
        CollectionAssert.AreEqual(padded, Reference.AesCbc(Reference.TitleKey.ToArray(), Reference.IndexIv(2), output.ToArray(), encrypt: false));
    }

    [TestMethod]
    public void EncryptPlain_ExactMultiple_HasNoExtraBlock()
    {
        var data = Reference.Pattern(2 * NusFormat.ContentPadding, 7);
        var output = new MemoryStream();
        using var encryptor = new ContentEncryptor(Reference.TitleKey);

        var record = encryptor.EncryptPlain(0, new MemoryStream(data), output, Plain, CancellationToken.None);

        Assert.AreEqual(data.Length, record.Size);
        CollectionAssert.AreEqual(data, Reference.AesCbc(Reference.TitleKey.ToArray(), new byte[16], output.ToArray(), encrypt: false));
    }

    [TestMethod]
    public void EncryptPlain_Cancelled_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        using var encryptor = new ContentEncryptor(Reference.TitleKey);

        Assert.ThrowsExactly<OperationCanceledException>(() => encryptor.EncryptPlain(0, new MemoryStream(new byte[10]), new MemoryStream(), Plain, cts.Token));
    }
}
