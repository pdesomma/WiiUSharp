using System.Text;

namespace WiiUSharp.Rpx.Tests;

/// <summary>
/// Builds a small plain (uncompressed) RPX with the section kinds a real one has.
/// </summary>
internal static class FakeRpx
{
    public const int BssSize = 0x1234;
    public const uint SectionHeadersOffset = 0x40;

    public static readonly byte[] Names = Encoding.ASCII.GetBytes("\0.text\0.rodata\0.bss\0.shstrtab\0.rplcrcs\0.rplfileinfo\0");
    public static readonly byte[] Rodata = Encoding.ASCII.GetBytes("WUP-JAAE\0\0\0\0tiny rodata that will not shrink");
    public static readonly byte[] FileInfo = Enumerable.Range(0, 0x60).Select(i => (byte)(i * 5)).ToArray();
    public static readonly byte[] Text = Enumerable.Range(0, 40000).Select(i => (byte)(i % 7)).ToArray();

    public static IReadOnlyList<(string Name, SectionType Type, byte[] Data, uint Size)> Sections => new[]
    {
        ("", SectionType.Null, Array.Empty<byte>(), 0u),
        (".text", SectionType.ProgBits, Text, (uint)Text.Length),
        (".rodata", SectionType.ProgBits, Rodata, (uint)Rodata.Length),
        (".bss", SectionType.NoBits, Array.Empty<byte>(), (uint)BssSize),
        (".shstrtab", SectionType.StrTab, Names, (uint)Names.Length),
        (".rplcrcs", SectionType.RplCrcs, new byte[7 * 4], 7u * 4),
        (".rplfileinfo", SectionType.RplFileInfo, FileInfo, (uint)FileInfo.Length),
    };

    public static byte[] Build()
    {
        var sections = Sections;
        var output = new MemoryStream();
        var header = new byte[RpxFormat.HeaderSize];
        header[0] = 0x7F;
        header[1] = 0x45;
        header[2] = 0x4C;
        header[3] = 0x46;
        header[4] = 1;
        header[5] = 2;
        header[6] = 1;
        header[7] = 0xCA;
        header[8] = 0xFE;
        Write16(header, 0x10, RpxFormat.RplType);
        Write16(header, 0x12, 0x14);
        Write32(header, 0x14, 1);
        Write32(header, 0x18, 0x02000000);
        Write32(header, 0x20, SectionHeadersOffset);
        Write16(header, 0x28, RpxFormat.HeaderSize);
        Write16(header, 0x2E, RpxFormat.SectionHeaderSize);
        Write16(header, 0x30, (ushort)sections.Count);
        Write16(header, 0x32, 4);
        output.Write(header, 0, header.Length);
        while (output.Position < SectionHeadersOffset + sections.Count * RpxFormat.SectionHeaderSize)
            output.WriteByte(0);

        var headers = new byte[sections.Count][];
        var dataStart = (uint)output.Position;
        var position = (dataStart + 0xFFu) & ~0xFFu;
        for (var i = 0; i < sections.Count; i++)
        {
            var (name, type, data, size) = sections[i];
            var shdr = new byte[RpxFormat.SectionHeaderSize];
            Write32(shdr, 0x00, NameOffset(name));
            Write32(shdr, 0x04, (uint)type);
            Write32(shdr, 0x08, type == SectionType.ProgBits ? 0x6u : 0u);
            Write32(shdr, 0x0C, 0x02000000u + (uint)i * 0x10000);
            Write32(shdr, 0x14, size);
            Write32(shdr, 0x20, 0x20);
            if (type != SectionType.Null && type != SectionType.NoBits)
            {
                Write32(shdr, 0x10, position);
                output.Position = position;
                output.Write(data, 0, data.Length);
                position = ((uint)output.Position + 0xFFu) & ~0xFFu;
            }
            headers[i] = shdr;
        }
        output.Position = SectionHeadersOffset;
        foreach (var shdr in headers)
            output.Write(shdr, 0, shdr.Length);
        return output.ToArray();
    }

    public static uint NameOffset(string name)
    {
        if (name.Length == 0)
            return 0;
        var needle = Encoding.ASCII.GetBytes(name + "\0");
        for (var i = 0; i + needle.Length <= Names.Length; i++)
            if (Names.Skip(i).Take(needle.Length).SequenceEqual(needle))
                return (uint)i;
        throw new ArgumentException(name);
    }

    private static void Write16(byte[] bytes, int offset, ushort value)
    {
        bytes[offset] = (byte)(value >> 8);
        bytes[offset + 1] = (byte)value;
    }

    private static void Write32(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)(value >> 24);
        bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8);
        bytes[offset + 3] = (byte)value;
    }
}
