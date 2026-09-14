using System.Globalization;

namespace WiiUSharp.Nus;

/// <summary>
/// Sizes, names and magic values of the NUS package layout.
/// </summary>
public static class NusFormat
{
    /// <summary>
    /// Certificate chain length.
    /// </summary>
    public const int CertificateChainSize = 0xA00;
    /// <summary>
    /// Certificate chain file name.
    /// </summary>
    public const string CertificateFileName = "title.cert";
    /// <summary>
    /// Files inside a content start on this boundary.
    /// </summary>
    public const int ContentAlignment = 0x20;
    /// <summary>
    /// Content info entries in the TMD.
    /// </summary>
    public const int ContentInfoCount = 0x40;
    /// <summary>
    /// Bytes per content info entry.
    /// </summary>
    public const int ContentInfoSize = 0x24;
    /// <summary>
    /// Plain contents and the FST are padded to this.
    /// </summary>
    public const int ContentPadding = 0x8000;
    /// <summary>
    /// Bytes per content record in the TMD.
    /// </summary>
    public const int ContentRecordSize = 0x30;
    /// <summary>
    /// Data bytes in each hashed block.
    /// </summary>
    public const int HashBlockDataSize = 0xFC00;
    /// <summary>
    /// Hash header bytes in each hashed block.
    /// </summary>
    public const int HashBlockHeaderSize = 0x400;
    /// <summary>
    /// Encrypted size of a hashed block.
    /// </summary>
    public const int HashBlockSize = HashBlockHeaderSize + HashBlockDataSize;
    /// <summary>
    /// Hashes grouped per tree node.
    /// </summary>
    public const int HashesPerGroup = 16;
    /// <summary>
    /// SHA-1 digest length.
    /// </summary>
    public const int HashSize = 0x14;
    /// <summary>
    /// AES-128 key length.
    /// </summary>
    public const int KeySize = 16;
    /// <summary>
    /// A one-content rule splits past this many bytes.
    /// </summary>
    public const long MaxContentLength = (long)(0xBFFFFFFFL * 0.975);
    /// <summary>
    /// Ticket file name.
    /// </summary>
    public const string TicketFileName = "title.tik";
    /// <summary>
    /// Ticket length.
    /// </summary>
    public const int TicketSize = 0x350;
    /// <summary>
    /// TMD file name.
    /// </summary>
    public const string TmdFileName = "title.tmd";
    /// <summary>
    /// TMD bytes before the content info table.
    /// </summary>
    public const int TmdHeaderSize = 0x204;

    /// <summary>
    /// Name of a content file.
    /// </summary>
    /// <param name="index">Content index.</param>
    public static string ContentFileName(int index) => index.ToString("X8", CultureInfo.InvariantCulture) + ".app";

    /// <summary>
    /// Name of the H3 table of a hashed content.
    /// </summary>
    /// <param name="index">Content index.</param>
    public static string HashFileName(int index) => index.ToString("X8", CultureInfo.InvariantCulture) + ".h3";
}
