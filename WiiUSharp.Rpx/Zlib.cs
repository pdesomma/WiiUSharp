using System.IO.Compression;

namespace WiiUSharp.Rpx;

/// <summary>
/// zlib-wrapped deflate on every target framework.
/// </summary>
internal static class Zlib
{
    private const int HeaderSize = 2;
    private const int TrailerSize = 4;

    public static byte[] Compress(byte[] data)
    {
        var output = new MemoryStream();
        output.WriteByte(0x78);
        output.WriteByte(0x9C);
        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
            deflate.Write(data, 0, data.Length);
        var adler = Adler32(data);
        output.WriteByte((byte)(adler >> 24));
        output.WriteByte((byte)(adler >> 16));
        output.WriteByte((byte)(adler >> 8));
        output.WriteByte((byte)adler);
        return output.ToArray();
    }

    public static byte[] Decompress(byte[] data, int offset, int count, int expectedLength)
    {
        if (count < HeaderSize + TrailerSize)
            throw new InvalidDataException("zlib stream is too short.");
        if ((data[offset] & 0x0F) != 8)
            throw new InvalidDataException("zlib stream does not use deflate.");

        var output = new MemoryStream(expectedLength);
        using (var input = new MemoryStream(data, offset + HeaderSize, count - HeaderSize - TrailerSize))
        using (var inflate = new DeflateStream(input, CompressionMode.Decompress))
            inflate.CopyTo(output);
        if (output.Length != expectedLength)
            throw new InvalidDataException($"Inflated {output.Length} bytes, expected {expectedLength}.");
        return output.ToArray();
    }

    private static uint Adler32(byte[] data)
    {
        uint a = 1, b = 0;
        foreach (var value in data)
        {
            a = (a + value) % 65521;
            b = (b + a) % 65521;
        }
        return b << 16 | a;
    }
}
