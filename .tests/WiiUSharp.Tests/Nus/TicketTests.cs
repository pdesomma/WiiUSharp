namespace WiiUSharp.Nus.Tests;

[TestClass]
public class TicketTests
{
    [TestMethod]
    public void Build_Always_IsTicketSized()
    {
        Assert.AreEqual(NusFormat.TicketSize, Ticket.Build(Reference.TitleId, Reference.TitleKey, Reference.CommonKey).Length);
    }

    [TestMethod]
    public void Build_Always_WritesFixedFields()
    {
        var ticket = Ticket.Build(Reference.TitleId, Reference.TitleKey, Reference.CommonKey);

        Assert.AreEqual(0x00010004u, Reference.ReadUInt32(ticket, 0));
        CollectionAssert.AreEqual(Reference.Ascii("Root-CA00000003-XS0000000c"), Reference.Slice(ticket, 0x140, 26));
        CollectionAssert.AreEqual(new byte[6], Reference.Slice(ticket, 0x15A, 6));
        Assert.AreEqual(0x01, ticket[0x1BC]);
        Assert.AreEqual(0x00, ticket[0x1CF]);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x05 }, Reference.Slice(ticket, 0x1D0, 2));
        CollectionAssert.AreEqual(new byte[4], Reference.Slice(ticket, 0x1D8, 4));
        Assert.AreEqual(Reference.TitleId.Value, Reference.ReadUInt64(ticket, Ticket.TitleIdOffset));
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0x11, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x05 }, Reference.Slice(ticket, 0x1E4, 16));
        CollectionAssert.AreEqual(new byte[0xB0], Reference.Slice(ticket, 0x1F4, 0xB0));
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x01, 0x00, 0x14, 0x00, 0x00, 0x00, 0xAC }, Reference.Slice(ticket, 0x2A4, 8));
        CollectionAssert.AreEqual(new byte[] { 0xFF, 0xFF, 0xFF, 0x01 }, Reference.Slice(ticket, 0x2D0, 4));
        CollectionAssert.AreEqual(new byte[0x7C], Reference.Slice(ticket, 0x2D4, 0x7C));
    }

    [TestMethod]
    public void Build_TwoCalls_DifferInSignatureAndTicketId()
    {
        var a = Ticket.Build(Reference.TitleId, Reference.TitleKey, Reference.CommonKey);
        var b = Ticket.Build(Reference.TitleId, Reference.TitleKey, Reference.CommonKey);

        CollectionAssert.AreNotEqual(Reference.Slice(a, 4, 0x100), Reference.Slice(b, 4, 0x100));
        CollectionAssert.AreNotEqual(Reference.Slice(a, 0x1D2, 6), Reference.Slice(b, 0x1D2, 6));
    }

    [TestMethod]
    public void EncryptTitleKey_ValidKeys_DecryptsWithTitleIdIv()
    {
        var encrypted = Ticket.EncryptTitleKey(Reference.TitleId, Reference.TitleKey, Reference.CommonKey);
        var ticket = Ticket.Build(Reference.TitleId, Reference.TitleKey, Reference.CommonKey);

        var iv = new byte[16];
        var id = BitConverter.GetBytes(Reference.TitleId.Value);
        Array.Reverse(id);
        id.CopyTo(iv, 0);
        CollectionAssert.AreEqual(Reference.TitleKey.ToArray(), Reference.AesCbc(Reference.CommonKey.ToArray(), iv, encrypted, encrypt: false));
        CollectionAssert.AreEqual(encrypted, Reference.Slice(ticket, Ticket.EncryptedTitleKeyOffset, 16));
    }

    [TestMethod]
    public void Parse_BuiltTicket_ReturnsTitleIdAndWrappedKey()
    {
        var info = Ticket.Parse(Ticket.Build(Reference.TitleId, Reference.TitleKey, Reference.CommonKey));

        Assert.AreEqual(Reference.TitleId, info.TitleId);
        Assert.AreEqual(Reference.TitleKey.Encrypt(Reference.TitleId, Reference.CommonKey), info.TitleKey);
        Assert.AreEqual(Reference.TitleKey, info.TitleKey.Decrypt(Reference.TitleId, Reference.CommonKey));
        Assert.ThrowsExactly<InvalidDataException>(() => Ticket.Parse(new byte[0x100]));
        Assert.ThrowsExactly<ArgumentNullException>(() => Ticket.Parse(null!));
    }

    [TestMethod]
    public void Build_WrappedKey_MatchesBuildFromPlainKey()
    {
        var wrapped = Reference.TitleKey.Encrypt(Reference.TitleId, Reference.CommonKey);

        var ticket = Ticket.Build(Reference.TitleId, wrapped);

        Assert.AreEqual(NusFormat.TicketSize, ticket.Length);
        CollectionAssert.AreEqual(wrapped.ToArray(), Reference.Slice(ticket, Ticket.EncryptedTitleKeyOffset, 16));
        Assert.AreEqual(Reference.TitleId.Value, Reference.ReadUInt64(ticket, Ticket.TitleIdOffset));
        var fromPlain = Ticket.Build(Reference.TitleId, Reference.TitleKey, Reference.CommonKey);
        CollectionAssert.AreEqual(Reference.Slice(fromPlain, 0x140, 0x92), Reference.Slice(ticket, 0x140, 0x92));
        CollectionAssert.AreEqual(Reference.Slice(fromPlain, 0x1D8, 0x178), Reference.Slice(ticket, 0x1D8, 0x178));
    }

    [TestMethod]
    public void EncryptedTitleKey_ParseAndHex_RoundTrip()
    {
        var key = EncryptedTitleKey.Parse("00112233445566778899AABBCCDDEEFF");

        Assert.AreEqual("00112233445566778899aabbccddeeff", key.ToString());
        Assert.AreEqual(key, new EncryptedTitleKey(key.ToArray()));
        Assert.IsTrue(key != default);
        Assert.AreEqual("00000000000000000000000000000000", default(EncryptedTitleKey).ToString());
        Assert.AreEqual(Reference.TitleKey.ToString(), string.Concat(Reference.TitleKey.ToArray().Select(b => b.ToString("x2"))));
        Assert.AreEqual(Reference.CommonKey.ToString(), string.Concat(Reference.CommonKey.ToArray().Select(b => b.ToString("x2"))));
        Assert.ThrowsExactly<FormatException>(() => EncryptedTitleKey.Parse("abc"));
        Assert.ThrowsExactly<ArgumentException>(() => new EncryptedTitleKey(new byte[3]));
        Assert.ThrowsExactly<ArgumentNullException>(() => new EncryptedTitleKey(null!));
    }
}
