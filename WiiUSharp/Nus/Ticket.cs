using System.Security.Cryptography;
using System.Text;

namespace WiiUSharp.Nus;

/// <summary>
/// Builds and reads the title.tik.
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
    /// Builds a ticket around an already wrapped title key; no common key needed.
    /// </summary>
    /// <param name="titleId">Title the ticket is for.</param>
    /// <param name="titleKey">Wrapped key as title key lists carry it.</param>
    public static byte[] Build(TitleId titleId, EncryptedTitleKey titleKey)
    {
        var random = new byte[0x100 + 6];
        using (var generator = RandomNumberGenerator.Create())
            generator.GetBytes(random);
        return Build(titleId, titleKey.ToArray(), random);
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

    /// <summary>
    /// Reads the title ID and wrapped title key.
    /// </summary>
    /// <param name="ticket">Ticket bytes.</param>
    /// <exception cref="InvalidDataException">Too short to be a ticket.</exception>
    public static TicketInfo Parse(byte[] ticket)
    {
        if (ticket is null)
            throw new ArgumentNullException(nameof(ticket));
        if (ticket.Length < TitleIdOffset + 8)
            throw new InvalidDataException($"Ticket is {ticket.Length} bytes; expected at least {TitleIdOffset + 8}.");

        var key = new byte[NusFormat.KeySize];
        Array.Copy(ticket, EncryptedTitleKeyOffset, key, 0, key.Length);
        return new TicketInfo(new TitleId(BigEndian.ReadUInt64(ticket, TitleIdOffset)), new EncryptedTitleKey(key));
    }

    internal static byte[] Build(TitleId titleId, TitleKey titleKey, CommonKey commonKey, byte[] random) =>
        Build(titleId, EncryptTitleKey(titleId, titleKey, commonKey), random);

    private static byte[] Build(TitleId titleId, byte[] encryptedTitleKey, byte[] random)
    {
        var ticket = new byte[NusFormat.TicketSize];
        BigEndian.Write(ticket, 0x000, 0x00010004u);
        Array.Copy(random, 0, ticket, 0x004, 0x100);
        Encoding.ASCII.GetBytes(Issuer).CopyTo(ticket, 0x140);
        ticket[0x1BC] = 0x01;
        encryptedTitleKey.CopyTo(ticket, EncryptedTitleKeyOffset);
        ticket[0x1D1] = 0x05;
        Array.Copy(random, 0x100, ticket, 0x1D2, 6);
        BigEndian.Write(ticket, TitleIdOffset, titleId.Value);
        Rights.CopyTo(ticket, 0x1E4);
        ContentIndexHeader.CopyTo(ticket, 0x2A4);
        return ticket;
    }
}
