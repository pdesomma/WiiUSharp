using System.Text;

namespace WiiUSharp.Rpx;

/// <summary>
/// An RPX or RPL held in memory with every section plain; saves compressed or not.
/// </summary>
public sealed class RpxFile
{
    private RpxFile(ElfHeader header, IReadOnlyList<Section> sections)
    {
        Header = header;
        Sections = sections;
    }

    /// <summary>
    /// ELF header as read.
    /// </summary>
    public ElfHeader Header { get; }
    /// <summary>
    /// Sections in header order.
    /// </summary>
    public IReadOnlyList<Section> Sections { get; }

    /// <summary>
    /// The section named in the section-name string table, or null.
    /// </summary>
    /// <param name="name">Name such as .rodata.</param>
    public Section? FindSection(string name)
    {
        if (name is null)
            throw new ArgumentNullException(nameof(name));

        return Sections.FirstOrDefault(s => NameOf(s) == name);
    }

    /// <summary>
    /// Reads a file, inflating compressed sections.
    /// </summary>
    /// <param name="path">File path.</param>
    public static RpxFile Load(string path)
    {
        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    /// <summary>
    /// Reads a stream to its end, inflating compressed sections.
    /// </summary>
    /// <param name="stream">Source.</param>
    /// <exception cref="InvalidDataException">Not an RPX or RPL, or a section lies outside the file.</exception>
    public static RpxFile Load(Stream stream)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));

        var bytes = ReadAll(stream);
        return Parse(bytes);
    }

    /// <summary>
    /// Name of a section from the section-name string table; empty when there is none.
    /// </summary>
    /// <param name="section">Section to name.</param>
    public string NameOf(Section section)
    {
        if (section is null)
            throw new ArgumentNullException(nameof(section));
        if (Header.SectionNamesIndex >= Sections.Count)
            return "";

        var names = Sections[Header.SectionNamesIndex].Data;
        if (section.NameOffset >= names.Length)
            return "";
        var end = Array.IndexOf(names, (byte)0, (int)section.NameOffset);
        if (end < 0)
            end = names.Length;
        return Encoding.ASCII.GetString(names, (int)section.NameOffset, end - (int)section.NameOffset);
    }

    /// <summary>
    /// Parses a whole file held in memory.
    /// </summary>
    /// <param name="bytes">File bytes.</param>
    /// <exception cref="InvalidDataException">Not an RPX or RPL, or a section lies outside the file.</exception>
    public static RpxFile Parse(byte[] bytes)
    {
        if (bytes is null)
            throw new ArgumentNullException(nameof(bytes));

        var header = ElfHeader.Parse(bytes);
        var sections = new List<Section>(header.SectionCount);
        for (var i = 0; i < header.SectionCount; i++)
        {
            var at = checked((int)(header.SectionHeadersOffset + i * RpxFormat.SectionHeaderSize));
            if (at + RpxFormat.SectionHeaderSize > bytes.Length)
                throw new InvalidDataException($"Section header {i} lies outside the file.");
            var raw = new byte[RpxFormat.SectionHeaderSize];
            Array.Copy(bytes, at, raw, 0, raw.Length);
            sections.Add(ReadSection(bytes, raw, i));
        }
        return new RpxFile(header, sections);
    }

    /// <summary>
    /// Writes the file.
    /// </summary>
    /// <param name="path">Destination.</param>
    /// <param name="compress">Deflate the sections the console expects compressed.</param>
    public void Save(string path, bool compress)
    {
        using var stream = File.Create(path);
        Save(stream, compress);
    }

    /// <summary>
    /// Writes the file: header, section table, then each section in its original file order aligned to 0x40, with the CRC table rebuilt.
    /// </summary>
    /// <param name="stream">Destination.</param>
    /// <param name="compress">Deflate the sections the console expects compressed.</param>
    public void Save(Stream stream, bool compress)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));

        var output = new MemoryStream();
        var headerBytes = Header.ToBytes();
        output.Write(headerBytes, 0, headerBytes.Length);
        var tableEnd = Header.SectionHeadersOffset + (long)Header.SectionCount * RpxFormat.SectionHeaderSize;
        Pad(output, Math.Max(tableEnd, RpxFormat.DataAlignment));

        var placed = new byte[Sections.Count][];
        var crcs = new uint[Sections.Count];
        var crcTableOffset = -1L;
        var ordered = Sections.Select((s, i) => (Section: s, Index: i)).Where(p => p.Section.HasData).OrderBy(p => p.Section.StoredOffset).ToArray();
        foreach (var (section, index) in ordered)
        {
            var offset = (uint)output.Position;
            if (section.Type == SectionType.NoBits)
            {
                placed[index] = section.HeaderBytes(offset, section.StoredSize, compressed: false);
                continue;
            }
            var data = section.Data;
            crcs[index] = Crc32.Compute(data);
            var deflated = compress ? Deflate(section, data) : null;
            if (deflated is not null)
            {
                var length = new byte[4];
                BigEndian.Write(length, 0, (uint)data.Length);
                output.Write(length, 0, 4);
                output.Write(deflated, 0, deflated.Length);
                placed[index] = section.HeaderBytes(offset, (uint)(deflated.Length + 4), compressed: true);
            }
            else
            {
                output.Write(data, 0, data.Length);
                placed[index] = section.HeaderBytes(offset, (uint)data.Length, compressed: false);
            }
            Pad(output, (output.Position + RpxFormat.DataAlignment - 1) / RpxFormat.DataAlignment * RpxFormat.DataAlignment);
            if (section.Type == SectionType.RplCrcs)
            {
                crcs[index] = 0;
                crcTableOffset = offset;
            }
        }
        for (var i = 0; i < Sections.Count; i++)
            placed[i] ??= Sections[i].HeaderBytes(0, Sections[i].StoredSize, compressed: false);

        output.Position = Header.SectionHeadersOffset;
        foreach (var bytes in placed)
            output.Write(bytes, 0, bytes.Length);
        if (crcTableOffset >= 0)
        {
            output.Position = crcTableOffset;
            var table = new byte[4];
            foreach (var crc in crcs)
            {
                BigEndian.Write(table, 0, crc);
                output.Write(table, 0, 4);
            }
        }
        output.Position = 0;
        output.CopyTo(stream);
    }

    private static void Pad(MemoryStream output, long to)
    {
        while (output.Position < to)
            output.WriteByte(0);
    }

    private static byte[] ReadAll(Stream stream)
    {
        var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static Section ReadSection(byte[] file, byte[] header, int index)
    {
        var type = (SectionType)BigEndian.ReadUInt32(header, 0x04);
        var flags = BigEndian.ReadUInt32(header, 0x08);
        var offset = BigEndian.ReadUInt32(header, 0x10);
        var size = BigEndian.ReadUInt32(header, 0x14);
        var compressed = (flags & RpxFormat.ZlibFlag) != 0;
        if (offset == 0 || type == SectionType.NoBits)
            return new Section(header, Array.Empty<byte>(), compressed, offset);
        if ((long)offset + size > file.Length)
            throw new InvalidDataException($"Section {index} lies outside the file.");

        byte[] data;
        if (compressed)
        {
            var plainLength = BigEndian.ReadUInt32(file, (int)offset);
            data = Zlib.Decompress(file, (int)offset + 4, (int)size - 4, checked((int)plainLength));
        }
        else
        {
            data = new byte[size];
            Array.Copy(file, offset, data, 0, size);
        }
        return new Section(header, data, compressed, offset);
    }

    private static byte[]? Deflate(Section section, byte[] data)
    {
        if (section.Type == SectionType.RplFileInfo || section.Type == SectionType.RplCrcs)
            return null;

        var deflated = Zlib.Compress(data);
        return data.Length >= RpxFormat.ZlibThreshold || deflated.Length + 4 < data.Length ? deflated : null;
    }
}
