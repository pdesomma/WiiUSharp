using System.Security.Cryptography;

namespace WiiUSharp.Nus;

/// <summary>
/// Writes the encrypted .app (and .h3) for one content and reports what the TMD records.
/// </summary>
internal sealed class ContentEncryptor : IDisposable
{
    private readonly Aes128Cbc _aes;

    public ContentEncryptor(TitleKey key)
    {
        _aes = new Aes128Cbc(key.ToArray());
    }

    public void Dispose() => _aes.Dispose();

    /// <summary>
    /// Hashed layout: 0x400 hash header plus 0xFC00 data per block, H3 table alongside. Opens the plaintext twice.
    /// </summary>
    public ContentRecord EncryptHashed(int index, Func<Stream> open, Stream output, Stream h3Output, ContentType type, CancellationToken cancellationToken)
    {
        HashTree tree;
        using (var plain = open())
            tree = HashTree.Compute(plain, cancellationToken);

        var h3 = tree.H3Bytes();
        h3Output.Write(h3, 0, h3.Length);

        var data = new byte[NusFormat.HashBlockDataSize];
        using (var plain = open())
        {
            for (var block = 0; block < tree.BlockCount; block++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = Fill(plain, data);
                Array.Clear(data, read, data.Length - read);

                var header = tree.HeaderFor(block);
                header[1] ^= (byte)index;
                var encryptedHeader = _aes.Encrypt(header, Aes128Cbc.IvFor(index));
                var iv = new byte[NusFormat.KeySize];
                Array.Copy(tree.BlockHash(block), iv, iv.Length);
                var encryptedData = _aes.Encrypt(data, iv);

                output.Write(encryptedHeader, 0, encryptedHeader.Length);
                output.Write(encryptedData, 0, encryptedData.Length);
            }
        }

        return new ContentRecord(index, type, (long)tree.BlockCount * NusFormat.HashBlockSize, Sha1(h3));
    }

    /// <summary>
    /// Plain layout: one CBC chain over the plaintext padded to 0x8000.
    /// </summary>
    public ContentRecord EncryptPlain(int index, Stream plain, Stream output, ContentType type, CancellationToken cancellationToken)
    {
        var buffer = new byte[NusFormat.ContentPadding];
        var iv = Aes128Cbc.IvFor(index);
        long size = 0;
        using var sha = SHA1.Create();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = Fill(plain, buffer);
            if (read == 0 && size > 0)
                break;
            Array.Clear(buffer, read, buffer.Length - read);
            sha.TransformBlock(buffer, 0, buffer.Length, null, 0);
            iv = _aes.EncryptInPlace(buffer, buffer.Length, iv);
            output.Write(buffer, 0, buffer.Length);
            size += buffer.Length;
            if (read < buffer.Length)
                break;
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return new ContentRecord(index, type, size, sha.Hash!);
    }

    private static int Fill(Stream stream, byte[] buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0)
                break;
            total += read;
        }
        return total;
    }

    private static byte[] Sha1(byte[] data)
    {
        using var sha = SHA1.Create();
        return sha.ComputeHash(data);
    }
}
