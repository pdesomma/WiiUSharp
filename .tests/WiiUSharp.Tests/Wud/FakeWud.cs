using System.Text;
using WiiUSharp.Nus;
using WiiUSharp.Nus.Tests;

namespace WiiUSharp.Wud.Tests;

/// <summary>
/// A small disc image laid out like a retail one: plain header, disc-key-encrypted partition table, an SI partition with the game's ticket, TMD and certificates, and a GM partition holding the contents a <see cref="NusPacker"/> made.
/// </summary>
internal static class FakeWud
{
    public const string ProductCode = "WUP-P-TEST";
    public const long SystemPartitionOffset = 0x40000;
    public const long GamePartitionOffset = 0x100000;
    public static readonly DiscKey DiscKey = new(Enumerable.Range(1, 16).Select(i => (byte)(i * 17)).ToArray());
    public static readonly byte[] CertificateChain = Reference.Pattern(0xA00, 42);

    /// <summary>
    /// Packs the fake title, then lays the disc out around the package.
    /// </summary>
    /// <param name="root">Scratch folder; the package lands in it.</param>
    /// <param name="h3Words">Value of the partition header's word count ahead of the H3 tables.</param>
    /// <param name="withCertificates">Keep a title.cert in the SI partition.</param>
    /// <returns>The disc bytes and the package folder they were built from.</returns>
    public static (byte[] Disc, string Package) Build(string root, int h3Words = 2, bool withCertificates = true)
    {
        var package = Path.Combine(root, "package");
        var packed = new NusPacker(Reference.CommonKey).Pack(FakeTitle.Create(Path.Combine(root, "title")), package, Reference.TitleKey, certificateChain: CertificateChain);

        var fstPlain = Reference.AesCbc(Reference.TitleKey.ToArray(), new byte[16], File.ReadAllBytes(Path.Combine(package, NusFormat.ContentFileName(0))), encrypt: false);
        var fst = Fst.Parse(fstPlain);

        var game = new MemoryStream();
        game.Write(GamePartitionHeader(fst, packed, package, h3Words), 0, WudFormat.SectorSize);
        foreach (var record in packed.Contents)
        {
            var at = WudFormat.SectorSize + (long)fst.Contents[record.Index].Offset * WudFormat.SectorSize;
            if (game.Length < at)
                game.SetLength(at);
            game.Position = at;
            var bytes = File.ReadAllBytes(Path.Combine(package, NusFormat.ContentFileName(record.Id)));
            game.Write(bytes, 0, bytes.Length);
        }

        var system = SystemPartition(File.ReadAllBytes(Path.Combine(package, NusFormat.TicketFileName)), File.ReadAllBytes(Path.Combine(package, NusFormat.TmdFileName)), withCertificates ? CertificateChain : null);

        var disc = new MemoryStream();
        disc.Write(Encoding.ASCII.GetBytes(ProductCode), 0, ProductCode.Length);
        disc.Position = WudFormat.DiscMagicOffset;
        disc.Write(BigEndianBytes(WudFormat.DiscMagic), 0, 4);
        disc.Position = WudFormat.PartitionTableOffset;
        var table = PartitionTable(("SI", SystemPartitionOffset), ("GM" + Reference.TitleId.Value.ToString("X16") + "00", GamePartitionOffset));
        disc.Write(Reference.AesCbc(DiscKey.ToArray(), new byte[16], table, encrypt: true), 0, table.Length);
        disc.Position = SystemPartitionOffset;
        disc.Write(system, 0, system.Length);
        disc.Position = GamePartitionOffset;
        game.WriteTo(disc);
        return (disc.ToArray(), package);
    }

    /// <summary>
    /// The same disc in WUX form: sectors stored once, index in front.
    /// </summary>
    /// <param name="disc">Plain disc bytes.</param>
    /// <param name="sectorSize">WUX sector size.</param>
    public static byte[] Wux(byte[] disc, int sectorSize = 0x8000)
    {
        var count = (disc.Length + sectorSize - 1) / sectorSize;
        var stored = new List<byte[]>();
        var seen = new Dictionary<string, uint>();
        var index = new uint[count];
        for (var i = 0; i < count; i++)
        {
            var sector = new byte[sectorSize];
            Array.Copy(disc, i * sectorSize, sector, 0, Math.Min(sectorSize, disc.Length - i * sectorSize));
            var key = Convert.ToBase64String(sector);
            if (!seen.TryGetValue(key, out var at))
            {
                at = (uint)stored.Count;
                stored.Add(sector);
                seen[key] = at;
            }
            index[i] = at;
        }

        var output = new MemoryStream();
        output.Write(LittleEndianBytes(WuxStream.Magic0), 0, 4);
        output.Write(LittleEndianBytes(WuxStream.Magic1), 0, 4);
        output.Write(LittleEndianBytes((uint)sectorSize), 0, 4);
        output.Write(new byte[4], 0, 4);
        output.Write(LittleEndianBytes((uint)disc.Length), 0, 4);
        output.Write(new byte[4], 0, 4);
        output.Write(new byte[8], 0, 8);
        foreach (var entry in index)
            output.Write(LittleEndianBytes(entry), 0, 4);
        output.SetLength((output.Length + sectorSize - 1) / sectorSize * sectorSize);
        output.Position = output.Length;
        foreach (var sector in stored)
            output.Write(sector, 0, sector.Length);
        return output.ToArray();
    }

    private static byte[] BigEndianBytes(uint value) => new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value };

    /// <summary>
    /// Header: magic, size, the word count, then every hashed content's H3 table in order.
    /// </summary>
    private static byte[] GamePartitionHeader(Fst fst, NusPackage packed, string package, int h3Words)
    {
        var header = new byte[WudFormat.SectorSize];
        BigEndianBytes(WudFormat.PartitionMagic).CopyTo(header, 0);
        BigEndianBytes((uint)WudFormat.SectorSize).CopyTo(header, 0x04);
        BigEndianBytes((uint)h3Words).CopyTo(header, 0x10);
        var fstLength = File.ReadAllBytes(Path.Combine(package, NusFormat.ContentFileName(0))).Length;
        BigEndianBytes((uint)fstLength).CopyTo(header, 0x14);
        BigEndianBytes(1).CopyTo(header, 0x18);
        header[0x24] = 1;
        header[0x25] = 2;
        var at = WudFormat.PartitionHeaderFixedSize + h3Words * 4;
        foreach (var record in packed.Contents.Where(c => c.IsHashed))
        {
            var h3 = File.ReadAllBytes(Path.Combine(package, NusFormat.HashFileName(record.Id)));
            h3.CopyTo(header, at);
            at += h3.Length;
        }
        return header;
    }

    private static byte[] LittleEndianBytes(uint value) => new[] { (byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24) };

    private static byte[] PartitionTable(params (string Name, long Offset)[] partitions)
    {
        var table = new byte[WudFormat.SectorSize];
        BigEndianBytes(WudFormat.PartitionTableMagic).CopyTo(table, 0);
        BigEndianBytes((uint)WudFormat.SectorSize).CopyTo(table, 4);
        BigEndianBytes((uint)partitions.Length).CopyTo(table, 0x1C);
        for (var i = 0; i < partitions.Length; i++)
        {
            var at = WudFormat.PartitionEntriesOffset + i * WudFormat.PartitionEntrySize;
            Encoding.ASCII.GetBytes(partitions[i].Name).CopyTo(table, at);
            table[at + 0x1F] = 1;
            BigEndianBytes((uint)(partitions[i].Offset / WudFormat.SectorSize)).CopyTo(table, at + 0x20);
        }
        return table;
    }

    /// <summary>
    /// SI partition: header, an FST whose content 1 holds folder 00 with the three files, then that content raw-encrypted with the disc key.
    /// </summary>
    private static byte[] SystemPartition(byte[] ticket, byte[] tmd, byte[]? certificates)
    {
        var files = new List<(string Name, byte[] Bytes)> { (NusFormat.TicketFileName, ticket), (NusFormat.TmdFileName, tmd) };
        if (certificates is not null)
            files.Add((NusFormat.CertificateFileName, certificates));

        var data = new MemoryStream();
        var offsets = new List<int>();
        foreach (var (_, bytes) in files)
        {
            offsets.Add((int)data.Length);
            data.Write(bytes, 0, bytes.Length);
            data.SetLength((data.Length + 0x20 - 1) / 0x20 * 0x20);
            data.Position = data.Length;
        }
        var dataBytes = data.ToArray();
        Array.Resize(ref dataBytes, (dataBytes.Length + 15) / 16 * 16);

        var names = new MemoryStream();
        int Name(string text)
        {
            var at = (int)names.Length;
            var bytes = Encoding.ASCII.GetBytes(text);
            names.Write(bytes, 0, bytes.Length);
            names.WriteByte(0);
            return at;
        }
        var entries = new List<byte[]>();
        entries.Add(Entry(0x01, Name(""), 0, (uint)(2 + files.Count), 0, 0));
        entries.Add(Entry(0x01, Name("00"), 0, (uint)(2 + files.Count), 0, 0));
        for (var i = 0; i < files.Count; i++)
            entries.Add(Entry(0x00, Name(files[i].Name), (uint)offsets[i], (uint)files[i].Bytes.Length, Fst.UnshiftedOffsetFlag, 1));

        var fst = new MemoryStream();
        fst.Write(new byte[] { 0x46, 0x53, 0x54, 0x00 }, 0, 4);
        fst.Write(BigEndianBytes(0x20), 0, 4);
        fst.Write(BigEndianBytes(2), 0, 4);
        fst.Write(new byte[0x14], 0, 0x14);
        fst.Write(ContentEntry(0, 2, 1), 0, 0x20);
        fst.Write(ContentEntry(2, (uint)((dataBytes.Length + WudFormat.SectorSize - 1) / WudFormat.SectorSize), 1), 0, 0x20);
        foreach (var entry in entries)
            fst.Write(entry, 0, entry.Length);
        names.WriteTo(fst);
        var fstBytes = fst.ToArray();
        var fstPadded = (byte[])fstBytes.Clone();
        Array.Resize(ref fstPadded, (fstPadded.Length + 15) / 16 * 16);

        var partition = new byte[WudFormat.SectorSize * 3 + dataBytes.Length];
        BigEndianBytes(WudFormat.PartitionMagic).CopyTo(partition, 0);
        BigEndianBytes((uint)WudFormat.SectorSize).CopyTo(partition, 0x04);
        BigEndianBytes((uint)fstBytes.Length).CopyTo(partition, 0x14);
        BigEndianBytes(1).CopyTo(partition, 0x18);
        partition[0x25] = 1;
        Reference.AesCbc(DiscKey.ToArray(), new byte[16], fstPadded, encrypt: true).CopyTo(partition, WudFormat.SectorSize);
        Reference.AesCbc(DiscKey.ToArray(), Reference.IndexIv(1), dataBytes, encrypt: true).CopyTo(partition, WudFormat.SectorSize * 3);
        return partition;
    }

    private static byte[] ContentEntry(uint offset, uint size, byte hashMode)
    {
        var entry = new byte[0x20];
        BigEndianBytes(offset).CopyTo(entry, 0);
        BigEndianBytes(size).CopyTo(entry, 4);
        entry[0x14] = hashMode;
        return entry;
    }

    private static byte[] Entry(byte type, int nameOffset, uint offset, uint size, ushort flags, ushort content)
    {
        var entry = new byte[0x10];
        entry[0] = type;
        entry[1] = (byte)(nameOffset >> 16);
        entry[2] = (byte)(nameOffset >> 8);
        entry[3] = (byte)nameOffset;
        BigEndianBytes(offset).CopyTo(entry, 4);
        BigEndianBytes(size).CopyTo(entry, 8);
        entry[0xC] = (byte)(flags >> 8);
        entry[0xD] = (byte)flags;
        entry[0xE] = (byte)(content >> 8);
        entry[0xF] = (byte)content;
        return entry;
    }
}
