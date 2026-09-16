using System.Globalization;
using System.Text;
using WiiUSharp.Nus;

namespace WiiUSharp.Wud;

/// <summary>
/// A Wii U disc image opened with its disc key: the partition table, every partition's header, and the game titles the system partition describes.
/// </summary>
public sealed class WudDisc
{
    private const int MaxHeaderSize = 1 << 20;

    private WudDisc(Stream image, DiscKey key, string productCode, IReadOnlyList<WudPartition> partitions, IReadOnlyList<WudTitle> titles)
    {
        Image = image;
        Key = key;
        ProductCode = productCode;
        Partitions = partitions;
        Titles = titles;
    }

    /// <summary>
    /// The disc image the structures were read from; not owned.
    /// </summary>
    public Stream Image { get; }
    /// <summary>
    /// Key the table and system partition were unlocked with.
    /// </summary>
    public DiscKey Key { get; }
    /// <summary>
    /// Every partition in table order.
    /// </summary>
    public IReadOnlyList<WudPartition> Partitions { get; }
    /// <summary>
    /// Product code from the first bytes of the disc, e.g. WUP-P-ARDE.
    /// </summary>
    public string ProductCode { get; }
    /// <summary>
    /// The game titles on the disc: usually the game, sometimes an update or DLC beside it.
    /// </summary>
    public IReadOnlyList<WudTitle> Titles { get; }

    /// <summary>
    /// Reads the disc structures.
    /// </summary>
    /// <param name="image">Seekable plain disc image, as <see cref="WudImage.Open"/> returns.</param>
    /// <param name="key">The disc key.</param>
    /// <exception cref="InvalidDataException">Not a Wii U disc, or a key that does not open it.</exception>
    public static WudDisc Read(Stream image, DiscKey key)
    {
        if (image is null)
            throw new ArgumentNullException(nameof(image));
        if (!image.CanSeek)
            throw new ArgumentException("Disc image must be seekable.", nameof(image));

        var productCode = Encoding.ASCII.GetString(ReadAt(image, 0, WudFormat.ProductCodeSize)).TrimEnd('\0');
        if (BigEndian.ReadUInt32(ReadAt(image, WudFormat.DiscMagicOffset, 4), 0) != WudFormat.DiscMagic)
            throw new InvalidDataException("Not a Wii U disc image.");

        using var aes = new Aes128Cbc(key.ToArray());
        var table = aes.Decrypt(ReadAt(image, WudFormat.PartitionTableOffset, WudFormat.SectorSize), new byte[NusFormat.KeySize]);
        if (BigEndian.ReadUInt32(table, 0) != WudFormat.PartitionTableMagic)
            throw new InvalidDataException("The disc key does not open this disc.");

        var count = checked((int)BigEndian.ReadUInt32(table, 0x1C));
        if (count < 1 || WudFormat.PartitionEntriesOffset + count * WudFormat.PartitionEntrySize > table.Length)
            throw new InvalidDataException("Partition table declares more entries than fit.");

        var partitions = new List<WudPartition>();
        for (var i = 0; i < count; i++)
        {
            var at = WudFormat.PartitionEntriesOffset + i * WudFormat.PartitionEntrySize;
            var name = Encoding.ASCII.GetString(table, at, WudFormat.PartitionNameSize).TrimEnd('\0');
            var offset = (long)BigEndian.ReadUInt32(table, at + 0x20) * WudFormat.SectorSize;
            partitions.Add(new WudPartition(i, name, offset, ReadHeader(image, name, offset)));
        }

        return new WudDisc(image, key, productCode, partitions, ReadTitles(image, aes, partitions));
    }

    /// <summary>
    /// Bytes of a file stored plain-encrypted in one of a partition's contents.
    /// </summary>
    internal static byte[] ReadRawFile(Stream image, Aes128Cbc aes, WudPartition partition, Fst fst, FstFile file)
    {
        var cluster = fst.Contents[file.ContentIndex];
        var end = checked((int)((file.Offset + file.Length + NusFormat.KeySize - 1) / NusFormat.KeySize * NusFormat.KeySize));
        var encrypted = ReadAt(image, partition.DataOffset + (long)cluster.Offset * WudFormat.SectorSize, end);
        var plain = aes.Decrypt(encrypted, Aes128Cbc.IvFor(file.ContentIndex));
        var bytes = new byte[file.Length];
        Array.Copy(plain, file.Offset, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>
    /// The partition's FST, decrypted with the key its header names.
    /// </summary>
    internal static Fst ReadFst(Stream image, Aes128Cbc aes, WudPartition partition)
    {
        var padded = (partition.FstSize + NusFormat.KeySize - 1) / NusFormat.KeySize * NusFormat.KeySize;
        var plain = aes.Decrypt(ReadAt(image, partition.FstOffset, padded), new byte[NusFormat.KeySize]);
        if (!Fst.HasMagic(plain))
            throw new InvalidDataException($"Partition {partition.Name}: the key does not open its FST.");
        Array.Resize(ref plain, partition.FstSize);
        return Fst.Parse(plain);
    }

    private static byte[] ReadAt(Stream image, long position, int count)
    {
        var bytes = new byte[count];
        image.Position = position;
        var read = 0;
        while (read < count)
        {
            var n = image.Read(bytes, read, count - read);
            if (n == 0)
                throw new EndOfStreamException("Disc image ends early.");
            read += n;
        }
        return bytes;
    }

    private static byte[] ReadHeader(Stream image, string name, long offset)
    {
        var head = ReadAt(image, offset, WudFormat.PartitionHeaderFixedSize);
        if (BigEndian.ReadUInt32(head, 0) != WudFormat.PartitionMagic)
            throw new InvalidDataException($"Partition {name} has no header at 0x{offset:X}.");
        var size = BigEndian.ReadUInt32(head, 0x04);
        if (size < WudFormat.PartitionHeaderFixedSize || size > MaxHeaderSize)
            throw new InvalidDataException($"Partition {name} declares a {size}-byte header.");
        return ReadAt(image, offset, (int)size);
    }

    /// <summary>
    /// Pairs every ticket folder of the SI partition with the game partition its title ID names.
    /// </summary>
    private static IReadOnlyList<WudTitle> ReadTitles(Stream image, Aes128Cbc discAes, IReadOnlyList<WudPartition> partitions)
    {
        var system = partitions.FirstOrDefault(p => p.Name.StartsWith(WudFormat.SystemInformationPartition, StringComparison.Ordinal));
        if (system is null)
            return Array.Empty<WudTitle>();

        var fst = ReadFst(image, discAes, system);
        var titles = new List<WudTitle>();
        foreach (var ticketFile in fst.Files.Where(f => f.Path.EndsWith("/" + NusFormat.TicketFileName, StringComparison.Ordinal)))
        {
            var folder = ticketFile.Path.Substring(0, ticketFile.Path.Length - NusFormat.TicketFileName.Length);
            var ticket = ReadRawFile(image, discAes, system, fst, ticketFile);
            var tmdFile = fst.Files.FirstOrDefault(f => f.Path == folder + NusFormat.TmdFileName);
            if (tmdFile is null)
                continue;
            var tmd = ReadRawFile(image, discAes, system, fst, tmdFile);
            var certFile = fst.Files.FirstOrDefault(f => f.Path == folder + NusFormat.CertificateFileName);
            var cert = certFile is null ? null : ReadRawFile(image, discAes, system, fst, certFile);

            // the folder is the partition index in hex; the partition is also named after the ticket's title
            var titleId = Ticket.Parse(ticket).TitleId;
            var name = WudFormat.GamePartitionPrefix + titleId.Value.ToString("X16");
            var partition = partitions.FirstOrDefault(p => p.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                            ?? partitions.FirstOrDefault(p => p.IsGame && int.TryParse(folder.Trim('/'), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var index) && p.Index == index);
            if (partition is not null)
                titles.Add(new WudTitle(partition, ticket, tmd, cert));
        }
        return titles;
    }
}
