namespace WiiUSharp.Nus.Tests;

[TestClass]
public class ContentDecryptorTests
{
    private const ContentType Plain = ContentType.Content | ContentType.Encrypted;

    [TestMethod]
    public void OpenPlain_UnpaddedSize_ReturnsExactlySizeBytes()
    {
        var plain = Reference.Pattern(650, 3);
        var encrypted = Reference.AesCbc(Reference.TitleKey.ToArray(), Reference.IndexIv(1), Reference.Padded(plain, 16), encrypt: true);
        var record = new ContentRecord(1, Plain, 650, Reference.Sha1(plain));
        Assert.AreEqual(656, record.EncryptedSize);

        using var decryptor = new ContentDecryptor(Reference.TitleKey);
        using var stream = decryptor.OpenPlain(record, new MemoryStream(encrypted), null);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);

        CollectionAssert.AreEqual(plain, memory.ToArray());
    }

    [TestMethod]
    public void OpenPlain_PaddedSize_HashesTheWholePaddedContent()
    {
        var plain = Reference.Padded(Reference.Pattern(100, 4), NusFormat.ContentPadding);
        var encrypted = Reference.AesCbc(Reference.TitleKey.ToArray(), Reference.IndexIv(2), plain, encrypt: true);
        var record = new ContentRecord(2, Plain, plain.Length, Reference.Sha1(plain));

        using var decryptor = new ContentDecryptor(Reference.TitleKey);
        using var stream = decryptor.OpenPlain(record, new MemoryStream(encrypted), null);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);

        CollectionAssert.AreEqual(plain, memory.ToArray());
    }

    [TestMethod]
    public void OpenPlain_WrongHash_ThrowsOnceTheLastBlockIsRead()
    {
        var plain = Reference.Pattern(64, 5);
        var encrypted = Reference.AesCbc(Reference.TitleKey.ToArray(), Reference.IndexIv(1), plain, encrypt: true);
        var record = new ContentRecord(1, Plain, 64, new byte[20]);

        using var decryptor = new ContentDecryptor(Reference.TitleKey);
        using var stream = decryptor.OpenPlain(record, new MemoryStream(encrypted), null);
        var buffer = new byte[32];

        Assert.ThrowsExactly<InvalidDataException>(() => stream.Read(buffer, 0, 32));
    }

    [TestMethod]
    public void PeekPlain_ShortContent_ThrowsInvalidDataException()
    {
        using var decryptor = new ContentDecryptor(Reference.TitleKey);

        Assert.ThrowsExactly<InvalidDataException>(() => decryptor.PeekPlain(new ContentRecord(0, Plain, 8, new byte[20]), new MemoryStream(new byte[8])));
    }
}
