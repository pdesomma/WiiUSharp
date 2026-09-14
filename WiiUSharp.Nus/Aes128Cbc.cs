using System.Security.Cryptography;

namespace WiiUSharp.Nus;

/// <summary>
/// AES-128-CBC without padding, as every NUS structure uses it.
/// </summary>
internal sealed class Aes128Cbc : IDisposable
{
    private readonly Aes _aes;

    public Aes128Cbc(byte[] key)
    {
        _aes = Aes.Create();
        _aes.Mode = CipherMode.CBC;
        _aes.Padding = PaddingMode.None;
        _aes.Key = key;
    }

    public void Dispose() => _aes.Dispose();

    public byte[] Decrypt(byte[] data, byte[] iv) => Decrypt(data, 0, data.Length, iv);

    public byte[] Decrypt(byte[] data, int offset, int count, byte[] iv)
    {
        using var transform = _aes.CreateDecryptor(_aes.Key, iv);
        return transform.TransformFinalBlock(data, offset, count);
    }

    /// <summary>
    /// Decrypts the first count bytes in place and returns the last sixteen ciphertext bytes for chaining.
    /// </summary>
    public byte[] DecryptInPlace(byte[] buffer, int count, byte[] iv)
    {
        var next = new byte[NusFormat.KeySize];
        Array.Copy(buffer, count - NusFormat.KeySize, next, 0, NusFormat.KeySize);
        var output = Decrypt(buffer, 0, count, iv);
        Array.Copy(output, buffer, count);
        return next;
    }

    public byte[] Encrypt(byte[] data, byte[] iv) => Encrypt(data, 0, data.Length, iv);

    public byte[] Encrypt(byte[] data, int offset, int count, byte[] iv)
    {
        using var transform = _aes.CreateEncryptor(_aes.Key, iv);
        return transform.TransformFinalBlock(data, offset, count);
    }

    /// <summary>
    /// Encrypts the first count bytes in place and returns the last sixteen for chaining.
    /// </summary>
    public byte[] EncryptInPlace(byte[] buffer, int count, byte[] iv)
    {
        var output = Encrypt(buffer, 0, count, iv);
        Array.Copy(output, buffer, count);
        var next = new byte[NusFormat.KeySize];
        Array.Copy(buffer, count - NusFormat.KeySize, next, 0, NusFormat.KeySize);
        return next;
    }

    public static byte[] IvFor(int contentIndex)
    {
        var iv = new byte[NusFormat.KeySize];
        BigEndian.Write(iv, 0, (ushort)contentIndex);
        return iv;
    }

    public static byte[] IvFor(TitleId titleId)
    {
        var iv = new byte[NusFormat.KeySize];
        BigEndian.Write(iv, 0, titleId.Value);
        return iv;
    }
}
