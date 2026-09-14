using System.Security.Cryptography;
using System.Text;

namespace WiiUSharp.Nus;

/// <summary>
/// Builds the fake-signed title.tik.
/// </summary>
public static class Ticket
{
    /// <summary>
    /// Where the encrypted title key sits.
    /// </summary>
    public const int EncryptedTitleKeyOffset = 0x1BF;
    /// <summary>
    /// Where the title ID sits.
    /// </summary>
    public const int TitleIdOffset = 0x1DC;

    private const string Issuer = "Root-CA00000003-XS0000000c";
    private static readonly byte[] ContentIndexHeader =
    {
        0x00, 0x01, 0x00, 0x14, 0x00, 0x00, 0x00, 0xAC, 0x00, 0x00, 0x00, 0x14, 0x00, 0x01, 0x00, 0x14,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x84,
        0x00, 0x00, 0x00, 0x84, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0x01,
    };
    private static readonly byte[] Rights = { 0x00, 0x00, 0x00, 0x11, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05 };

    /// <summary>
    /// Builds a ticket with a random signature and ticket ID.
    /// </summary>
    /// <param name="titleId">Title the ticket is for.</param>
    /// <param name="titleKey">Key the contents are encrypted with.</param>
    /// <param name="commonKey">Key the title key is wrapped with.</param>
    public static byte[] Build(TitleId titleId, TitleKey titleKey, CommonKey commonKey)
    {
        var random = new byte[0x100 + 6];
        using (var generator = RandomNumberGenerator.Create())
            generator.GetBytes(random);
        return Build(titleId, titleKey, commonKey, random);
    }

    /// <summary>
    /// Encrypts a title key the way the ticket stores it.
    /// </summary>
    /// <param name="titleId">Title ID; forms the IV.</param>
    /// <param name="titleKey">Plain title key.</param>
    /// <param name="commonKey">Wrapping key.</param>
    public static byte[] EncryptTitleKey(TitleId titleId, TitleKey titleKey, CommonKey commonKey)
    {
        using var aes = new Aes128Cbc(commonKey.ToArray());
        return aes.Encrypt(titleKey.ToArray(), Aes128Cbc.IvFor(titleId));
    }

    internal static byte[] Build(TitleId titleId, TitleKey titleKey, CommonKey commonKey, byte[] random)
    {
        var ticket = new byte[NusFormat.TicketSize];
        BigEndian.Write(ticket, 0x000, 0x00010004u);
        Array.Copy(random, 0, ticket, 0x004, 0x100);
        Encoding.ASCII.GetBytes(Issuer).CopyTo(ticket, 0x140);
        ticket[0x1BC] = 0x01;
        EncryptTitleKey(titleId, titleKey, commonKey).CopyTo(ticket, EncryptedTitleKeyOffset);
        ticket[0x1D1] = 0x05;
        Array.Copy(random, 0x100, ticket, 0x1D2, 6);
        BigEndian.Write(ticket, TitleIdOffset, titleId.Value);
        Rights.CopyTo(ticket, 0x1E4);
        ContentIndexHeader.CopyTo(ticket, 0x2A4);
        return ticket;
    }
}
