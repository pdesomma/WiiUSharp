using System.Security.Cryptography;

namespace WiiUSharp.Nfs;

/// <summary>
/// Sector-level AES-128-CBC of the packed payload.
/// </summary>
public sealed class NfsCipher : IDisposable
{
    private readonly Aes _aes;

    /// <summary>
    /// Creates a new instance of the <see cref="NfsCipher"/> class.
    /// </summary>
    /// <param name="key">Container key.</param>
    public NfsCipher(NfsKey key)
    {
        _aes = Aes.Create();
        _aes.Mode = CipherMode.CBC;
        _aes.Padding = PaddingMode.None;
        _aes.KeySize = 128;
        _aes.BlockSize = 128;
        _aes.Key = key.ToArray();
    }

    /// <summary>
    /// Decrypts one packed sector in place.
    /// </summary>
    /// <param name="sectorIndex">Index within the packed payload.</param>
    /// <param name="data">Whole sector, or a trailing partial one; a multiple of 16 bytes.</param>
    public void Decrypt(long sectorIndex, byte[] data) => Transform(sectorIndex, data, encrypt: false);

    /// <inheritdoc/>
    public void Dispose() => _aes.Dispose();

    /// <summary>
    /// Encrypts one packed sector in place.
    /// </summary>
    /// <param name="sectorIndex">Index within the packed payload.</param>
    /// <param name="data">Whole sector, or a trailing partial one; a multiple of 16 bytes.</param>
    public void Encrypt(long sectorIndex, byte[] data) => Transform(sectorIndex, data, encrypt: true);

    /// <summary>
    /// IV used for a packed sector: zero before <see cref="NfsFormat.FirstCounterSector"/>, then a big-endian counter in the last four bytes.
    /// </summary>
    /// <param name="sectorIndex">Index within the packed payload.</param>
    public static byte[] IvFor(long sectorIndex)
    {
        if (sectorIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(sectorIndex));

        var iv = new byte[16];
        if (sectorIndex < NfsFormat.FirstCounterSector)
            return iv;

        var counter = (uint)(NfsFormat.InitialCounter + (sectorIndex - NfsFormat.FirstCounterSector));
        iv[12] = (byte)(counter >> 24);
        iv[13] = (byte)(counter >> 16);
        iv[14] = (byte)(counter >> 8);
        iv[15] = (byte)counter;
        return iv;
    }

    private void Transform(long sectorIndex, byte[] data, bool encrypt)
    {
        if (data is null)
            throw new ArgumentNullException(nameof(data));
        if (data.Length == 0 || data.Length > NfsFormat.SectorSize || data.Length % 16 != 0)
            throw new ArgumentException("Data must be a non-empty multiple of 16 bytes no longer than a sector.", nameof(data));

        var iv = IvFor(sectorIndex);
#if NET6_0_OR_GREATER
        var result = encrypt
            ? _aes.EncryptCbc(data, iv, PaddingMode.None)
            : _aes.DecryptCbc(data, iv, PaddingMode.None);
        result.CopyTo(data, 0);
#else
        using var transform = encrypt ? _aes.CreateEncryptor(_aes.Key, iv) : _aes.CreateDecryptor(_aes.Key, iv);
        transform.TransformBlock(data, 0, data.Length, data, 0);
#endif
    }
}
