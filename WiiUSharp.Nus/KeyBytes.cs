using System.Globalization;

namespace WiiUSharp.Nus;

internal static class KeyBytes
{
    public static byte[] Parse(string hex)
    {
        if (hex is null)
            throw new ArgumentNullException(nameof(hex));
        if (hex.Length != NusFormat.KeySize * 2)
            throw new FormatException($"Key must be {NusFormat.KeySize * 2} hex characters.");

        var bytes = new byte[NusFormat.KeySize];
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return bytes;
    }

    public static byte[] Validate(byte[] bytes)
    {
        if (bytes is null)
            throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length != NusFormat.KeySize)
            throw new ArgumentException($"Key must be {NusFormat.KeySize} bytes.", nameof(bytes));

        return (byte[])bytes.Clone();
    }
}
