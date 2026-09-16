using System.Security.Cryptography;
using WiiUSharp.Nus;

namespace WiiUSharp.Wud;

/// <summary>
/// A game title on a disc: its partition plus the ticket, TMD and certificate chain the system partition keeps for it. Its contents are the same encrypted blobs an installed copy holds, so a package is a straight copy.
/// </summary>
public sealed class WudTitle
{
    private const int CopyBufferSize = 1 << 20;

    private readonly byte[]? _certificateChain;
    private readonly byte[] _ticket;
    private readonly byte[] _tmd;

    /// <summary>
    /// Creates a new instance of the <see cref="WudTitle"/> class.
    /// </summary>
    /// <param name="partition">Partition that holds the contents.</param>
    /// <param name="ticket">Raw ticket.</param>
    /// <param name="tmd">Raw TMD.</param>
    /// <param name="certificateChain">Raw certificate chain, or null when the disc keeps none.</param>
    public WudTitle(WudPartition partition, byte[] ticket, byte[] tmd, byte[]? certificateChain)
    {
        Partition = partition ?? throw new ArgumentNullException(nameof(partition));
        _ticket = (byte[])(ticket ?? throw new ArgumentNullException(nameof(ticket))).Clone();
        _tmd = (byte[])(tmd ?? throw new ArgumentNullException(nameof(tmd))).Clone();
        _certificateChain = (byte[]?)certificateChain?.Clone();
        Ticket = Nus.Ticket.Parse(_ticket);
        Tmd = Nus.Tmd.Parse(_tmd);
    }

    /// <summary>
    /// True when the disc keeps a certificate chain for the title; otherwise a package gets the stub chain.
    /// </summary>
    public bool HasCertificateChain => _certificateChain is not null;
    /// <summary>
    /// Partition that holds the contents.
    /// </summary>
    public WudPartition Partition { get; }
    /// <summary>
    /// Bytes every content adds up to on the disc.
    /// </summary>
    public long Size => Tmd.Contents.Sum(c => c.EncryptedSize);
    /// <summary>
    /// Parsed ticket.
    /// </summary>
    public TicketInfo Ticket { get; }
    /// <summary>
    /// The title's ID.
    /// </summary>
    public TitleId TitleId => Ticket.TitleId;
    /// <summary>
    /// Parsed TMD.
    /// </summary>
    public TmdInfo Tmd { get; }

    /// <summary>
    /// Writes the title as an installable package: title.tmd, title.tik, title.cert, every content as it sits on the disc, and each hashed content's H3 table.
    /// </summary>
    /// <param name="image">The disc image.</param>
    /// <param name="commonKey">Unlocks the title key, which the FST that locates the contents is encrypted with.</param>
    /// <param name="outputDirectory">Package folder; created if missing.</param>
    /// <param name="progress">One line per file.</param>
    /// <param name="cancellationToken">Cancels between files.</param>
    /// <exception cref="InvalidDataException">The common key is wrong, or an H3 table does not match the TMD.</exception>
    public void WritePackage(Stream image, CommonKey commonKey, string outputDirectory, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (image is null)
            throw new ArgumentNullException(nameof(image));
        if (outputDirectory is null)
            throw new ArgumentNullException(nameof(outputDirectory));

        var titleKey = Ticket.TitleKey.Decrypt(TitleId, commonKey);
        Fst fst;
        using (var aes = new Aes128Cbc(titleKey.ToArray()))
            fst = WudDisc.ReadFst(image, aes, Partition);
        if (fst.Contents.Count < Tmd.Contents.Count)
            throw new InvalidDataException("The FST names fewer contents than the TMD.");

        Directory.CreateDirectory(outputDirectory);
        File.WriteAllBytes(Path.Combine(outputDirectory, NusFormat.TmdFileName), _tmd);
        File.WriteAllBytes(Path.Combine(outputDirectory, NusFormat.TicketFileName), _ticket);
        File.WriteAllBytes(Path.Combine(outputDirectory, NusFormat.CertificateFileName), _certificateChain ?? CertificateChain.Stub());

        var buffer = new byte[CopyBufferSize];
        var h3Cursor = 0;
        using var sha1 = SHA1.Create();
        foreach (var record in Tmd.Contents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cluster = fst.Contents[record.Index];
            progress?.Report($"Copying content {record.Id:X8} ({record.EncryptedSize:N0} bytes)");
            image.Position = Partition.DataOffset + (long)cluster.Offset * WudFormat.SectorSize;
            using (var output = new FileStream(Path.Combine(outputDirectory, NusFormat.ContentFileName(record.Id)), FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize))
                Copy(image, output, record.EncryptedSize, buffer, cancellationToken);

            if (!record.IsHashed)
                continue;
            var h3 = TakeH3(record, ref h3Cursor, sha1);
            File.WriteAllBytes(Path.Combine(outputDirectory, NusFormat.HashFileName(record.Id)), h3);
        }
    }

    private static void Copy(Stream source, Stream destination, long count, byte[] buffer, CancellationToken cancellationToken)
    {
        while (count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, count));
            if (read == 0)
                throw new EndOfStreamException("Disc image ends inside a content.");
            destination.Write(buffer, 0, read);
            count -= read;
        }
    }

    /// <summary>
    /// The next H3 table in the partition header: one hash per 4096 blocks, and dumps pad the count by one, so the TMD hash settles which length is right.
    /// </summary>
    private byte[] TakeH3(ContentRecord record, ref int cursor, SHA1 sha1)
    {
        var blocks = record.EncryptedSize / NusFormat.HashBlockSize;
        var groups = (blocks + 4095) / 4096;
        foreach (var count in new[] { blocks / 4096 + 1, groups })
        {
            var length = checked((int)count * NusFormat.HashSize);
            var h3 = Partition.H3Tables(cursor, length);
            if (sha1.ComputeHash(h3).SequenceEqual(record.Hash.Take(NusFormat.HashSize)))
            {
                cursor += length;
                return h3;
            }
        }
        throw new InvalidDataException($"Content {record.Id:X8}: no H3 table in the partition header matches the TMD.");
    }
}
