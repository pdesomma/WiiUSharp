using System.Security.Cryptography;
using System.Text;

namespace WiiUSharp.Nus.Tests;

/// <summary>
/// Independent crypto and byte helpers the tests check the library against.
/// </summary>
internal static class Reference
{
    public static readonly CommonKey CommonKey = new(Enumerable.Range(1, 16).Select(i => (byte)(i * 7)).ToArray());
    public static readonly TitleId TitleId = new(TitleType.Demo, 0x1ABCDE00);
    public static readonly TitleKey TitleKey = new(Enumerable.Range(1, 16).Select(i => (byte)(i * 11)).ToArray());

    public static byte[] AesCbc(byte[] key, byte[] iv, byte[] data, bool encrypt)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        aes.Key = key;
        aes.IV = iv;
        using var transform = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor();
        return transform.TransformFinalBlock(data, 0, data.Length);
    }

    public static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    public static byte[] IndexIv(int index) => new byte[] { (byte)(index >> 8), (byte)index, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

    public static byte[] Pattern(int length, int seed)
    {
        var bytes = new byte[length];
        for (var i = 0; i < length; i++)
            bytes[i] = (byte)(seed * 31 + i * 7);
        return bytes;
    }

    public static byte[] Padded(byte[] data, int alignment)
    {
        var length = (data.Length + alignment - 1) / alignment * alignment;
        var padded = new byte[Math.Max(length, alignment)];
        data.CopyTo(padded, 0);
        return padded;
    }

    public static uint ReadUInt32(byte[] bytes, int offset) =>
        (uint)(bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3]);

    public static ulong ReadUInt64(byte[] bytes, int offset) =>
        (ulong)ReadUInt32(bytes, offset) << 32 | ReadUInt32(bytes, offset + 4);

    public static ushort ReadUInt16(byte[] bytes, int offset) => (ushort)(bytes[offset] << 8 | bytes[offset + 1]);

    public static byte[] Sha1(byte[] data)
    {
        using var sha = SHA1.Create();
        return sha.ComputeHash(data);
    }

    public static byte[] Sha256(byte[] data)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(data);
    }

    public static byte[] Slice(byte[] bytes, int offset, int length) => bytes.Skip(offset).Take(length).ToArray();

    public static string TempRoot() =>
        Path.Combine(Path.GetTempPath(), "WiiUSharp.Nus.Tests", Guid.NewGuid().ToString("N"));
}
