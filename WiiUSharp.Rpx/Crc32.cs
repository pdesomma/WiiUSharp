namespace WiiUSharp.Rpx;

/// <summary>
/// Standard CRC-32 (zlib polynomial) as the RPL CRC table uses it.
/// </summary>
public static class Crc32
{
    private static readonly uint[] Table = BuildTable();

    /// <summary>
    /// CRC of the whole buffer.
    /// </summary>
    /// <param name="data">Bytes to hash.</param>
    public static uint Compute(byte[] data) => Update(0, data ?? throw new ArgumentNullException(nameof(data)), 0, data.Length);

    /// <summary>
    /// Continues a running CRC.
    /// </summary>
    /// <param name="crc">Previous value, 0 to start.</param>
    /// <param name="data">Bytes to add.</param>
    /// <param name="offset">First byte.</param>
    /// <param name="count">Byte count.</param>
    public static uint Update(uint crc, byte[] data, int offset, int count)
    {
        if (data is null)
            throw new ArgumentNullException(nameof(data));

        crc = ~crc;
        for (var i = offset; i < offset + count; i++)
            crc = (crc >> 8) ^ Table[(crc ^ data[i]) & 0xFF];
        return ~crc;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var c = i;
            for (var j = 0; j < 8; j++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[i] = c;
        }
        return table;
    }
}
