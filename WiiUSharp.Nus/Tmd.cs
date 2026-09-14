using System.Security.Cryptography;
using System.Text;

namespace WiiUSharp.Nus;

/// <summary>
/// Builds the fake-signed title.tmd.
/// </summary>
public static class Tmd
{
    /// <summary>
    /// Where the content records start.
    /// </summary>
    public const int ContentRecordsOffset = NusFormat.TmdHeaderSize + NusFormat.ContentInfoCount * NusFormat.ContentInfoSize;
    /// <summary>
    /// Where the title ID sits.
    /// </summary>
    public const int TitleIdOffset = 0x18C;

    private const string Issuer = "Root-CA00000003-CP0000000b";

    /// <summary>
    /// Builds a TMD for the given contents, in index order.
    /// </summary>
    /// <param name="info">Title values.</param>
    /// <param name="contents">One record per content, index order.</param>
    public static byte[] Build(TitleInfo info, IReadOnlyList<ContentRecord> contents)
    {
        if (info is null)
            throw new ArgumentNullException(nameof(info));
        if (contents is null)
            throw new ArgumentNullException(nameof(contents));
        if (contents.Count == 0 || contents.Count > ushort.MaxValue)
            throw new ArgumentException("Between one and 65535 contents are required.", nameof(contents));
        for (var i = 0; i < contents.Count; i++)
            if (contents[i].Index != i)
                throw new ArgumentException($"Content at position {i} has index {contents[i].Index}.", nameof(contents));

        var records = new byte[contents.Count * NusFormat.ContentRecordSize];
        for (var i = 0; i < contents.Count; i++)
            contents[i].ToBytes().CopyTo(records, i * NusFormat.ContentRecordSize);

        var infos = new byte[NusFormat.ContentInfoCount * NusFormat.ContentInfoSize];
        BigEndian.Write(infos, 0x02, (ushort)contents.Count);
        Sha256(records).CopyTo(infos, 0x04);

        var tmd = new byte[ContentRecordsOffset + records.Length];
        BigEndian.Write(tmd, 0x000, 0x00010004u);
        Encoding.ASCII.GetBytes(Issuer).CopyTo(tmd, 0x140);
        tmd[0x180] = 0x01;
        BigEndian.Write(tmd, 0x184, info.OsVersion);
        BigEndian.Write(tmd, TitleIdOffset, info.TitleId.Value);
        BigEndian.Write(tmd, 0x194, 0x00000100u);
        BigEndian.Write(tmd, 0x198, info.GroupId);
        BigEndian.Write(tmd, 0x19A, info.AppType);
        BigEndian.Write(tmd, 0x1DC, info.TitleVersion);
        BigEndian.Write(tmd, 0x1DE, (ushort)contents.Count);
        Sha256(infos).CopyTo(tmd, 0x1E4);
        infos.CopyTo(tmd, NusFormat.TmdHeaderSize);
        records.CopyTo(tmd, ContentRecordsOffset);
        return tmd;
    }

    private static byte[] Sha256(byte[] data)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(data);
    }
}
