using System.Security.Cryptography;

namespace WiiUSharp.Nus;

/// <summary>
/// Opens the plaintext of an encrypted .app as a forward-only stream, verifying hashes as it goes.
/// </summary>
internal sealed class ContentDecryptor : IDisposable
{
    private readonly Aes128Cbc _aes;

    public ContentDecryptor(TitleKey key)
    {
        _aes = new Aes128Cbc(key.ToArray());
    }

    public void Dispose() => _aes.Dispose();

    /// <summary>
    /// Plaintext of one content; read it to the end so the last hash check runs.
    /// </summary>
    /// <param name="record">What the TMD says about the content.</param>
    /// <param name="encrypted">The .app bytes.</param>
    /// <param name="h3">The .h3 table for a hashed content, or null to skip the H3 check.</param>
    public Stream OpenPlain(ContentRecord record, Stream encrypted, byte[]? h3) =>
        record.IsHashed
            ? new HashedContentStream(_aes, record, encrypted, h3)
            : new PlainContentStream(_aes, record, encrypted);

    /// <summary>
    /// Decrypts just the first sixteen bytes of a plain content.
    /// </summary>
    public byte[] PeekPlain(ContentRecord record, Stream encrypted)
    {
        var block = new byte[NusFormat.KeySize];
        var read = Fill(encrypted, block, block.Length);
        if (read < block.Length)
            throw new InvalidDataException($"Content {record.Id:X8} is shorter than one block.");
        return _aes.Decrypt(block, Aes128Cbc.IvFor(record.Index));
    }

    private static int Fill(Stream stream, byte[] buffer, int count)
    {
        var total = 0;
        while (total < count)
        {
            var read = stream.Read(buffer, total, count - total);
            if (read == 0)
                break;
            total += read;
        }
        return total;
    }

    private abstract class ContentStream : Stream
    {
        private readonly byte[] _plain;
        private int _available;
        private int _consumed;
        private long _position;

        protected ContentStream(Aes128Cbc aes, ContentRecord record, Stream encrypted, int blockSize)
        {
            Aes = aes;
            Record = record;
            Encrypted = encrypted;
            _plain = new byte[blockSize];
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        protected Aes128Cbc Aes { get; }
        protected Stream Encrypted { get; }
        protected ContentRecord Record { get; }
        protected long Remaining { get; set; }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var total = 0;
            while (count > 0)
            {
                if (_consumed == _available)
                {
                    if (Remaining == 0)
                        break;
                    _available = NextBlock(_plain);
                    _consumed = 0;
                    if (Remaining == 0)
                        Finish();
                }
                var chunk = Math.Min(count, _available - _consumed);
                Array.Copy(_plain, _consumed, buffer, offset, chunk);
                _consumed += chunk;
                offset += chunk;
                count -= chunk;
                total += chunk;
            }
            _position += total;
            return total;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected void FillEncrypted(byte[] buffer, int count)
        {
            if (Fill(Encrypted, buffer, count) < count)
                throw new InvalidDataException($"Content {Record.Id:X8} ends before the {Record.Size} bytes the TMD records.");
            Remaining -= count;
        }

        /// <summary>
        /// Runs after the last block; throw when the whole-content hash is wrong.
        /// </summary>
        protected abstract void Finish();

        /// <summary>
        /// Decrypts the next block into the buffer and returns how many plain bytes it holds.
        /// </summary>
        protected abstract int NextBlock(byte[] plain);

        protected static void Verify(byte[] actual, byte[] expected, int expectedOffset, string what)
        {
            for (var i = 0; i < NusFormat.HashSize; i++)
                if (actual[i] != expected[expectedOffset + i])
                    throw new InvalidDataException(what + " hash does not match; the key is wrong or the content is damaged.");
        }
    }

    /// <summary>
    /// One CBC chain over the whole content; SHA-1 of the first Size plaintext bytes must match the TMD.
    /// </summary>
    private sealed class PlainContentStream : ContentStream
    {
        private const int ChunkSize = 1 << 20;

        private readonly SHA1 _sha = SHA1.Create();
        private byte[] _iv;
        private long _plainRemaining;

        public PlainContentStream(Aes128Cbc aes, ContentRecord record, Stream encrypted)
            : base(aes, record, encrypted, ChunkSize)
        {
            Remaining = record.EncryptedSize;
            _plainRemaining = record.Size;
            _iv = Aes128Cbc.IvFor(record.Index);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _sha.Dispose();
            base.Dispose(disposing);
        }

        protected override void Finish()
        {
            _sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            Verify(_sha.Hash!, Record.Hash, 0, $"Content {Record.Id:X8}");
        }

        protected override int NextBlock(byte[] plain)
        {
            var count = (int)Math.Min(plain.Length, Remaining);
            FillEncrypted(plain, count);
            _iv = Aes.DecryptInPlace(plain, count, _iv);
            var usable = (int)Math.Min(count, _plainRemaining);
            _plainRemaining -= usable;
            _sha.TransformBlock(plain, 0, usable, null, 0);
            return usable;
        }
    }

    /// <summary>
    /// 0x400 hash header plus 0xFC00 data per block; H0 through H3 checked on every block.
    /// </summary>
    private sealed class HashedContentStream : ContentStream
    {
        private const int H1Offset = NusFormat.HashesPerGroup * NusFormat.HashSize;
        private const int H2Offset = H1Offset * 2;

        private readonly byte[] _block = new byte[NusFormat.HashBlockSize];
        private readonly byte[]? _h3;
        private readonly SHA1 _sha = SHA1.Create();
        private int _index;

        public HashedContentStream(Aes128Cbc aes, ContentRecord record, Stream encrypted, byte[]? h3)
            : base(aes, record, encrypted, NusFormat.HashBlockDataSize)
        {
            if (record.Size % NusFormat.HashBlockSize != 0)
                throw new InvalidDataException($"Content {record.Id:X8} size {record.Size} is not a whole number of hashed blocks.");
            Remaining = record.Size;
            _h3 = h3;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _sha.Dispose();
            base.Dispose(disposing);
        }

        protected override void Finish()
        {
        }

        protected override int NextBlock(byte[] plain)
        {
            FillEncrypted(_block, _block.Length);
            var header = Aes.Decrypt(_block, 0, NusFormat.HashBlockHeaderSize, Aes128Cbc.IvFor(Record.Index));
            header[1] ^= (byte)Record.Index;

            var h0 = _index % NusFormat.HashesPerGroup * NusFormat.HashSize;
            var iv = new byte[NusFormat.KeySize];
            Array.Copy(header, h0, iv, 0, iv.Length);
            var data = Aes.Decrypt(_block, NusFormat.HashBlockHeaderSize, NusFormat.HashBlockDataSize, iv);
            var where = $"Content {Record.Id:X8} block {_index}";
            Verify(_sha.ComputeHash(data), header, h0, where + " H0");
            Verify(_sha.ComputeHash(header, 0, H1Offset), header, H1Offset + _index / 16 % 16 * NusFormat.HashSize, where + " H1");
            Verify(_sha.ComputeHash(header, H1Offset, H1Offset), header, H2Offset + _index / 256 % 16 * NusFormat.HashSize, where + " H2");
            if (_h3 is not null)
            {
                var h3 = _index / 4096 * NusFormat.HashSize;
                if (h3 + NusFormat.HashSize > _h3.Length)
                    throw new InvalidDataException($"Content {Record.Id:X8} H3 table is too short for block {_index}.");
                Verify(_sha.ComputeHash(header, H2Offset, H1Offset), _h3, h3, where + " H3");
            }

            data.CopyTo(plain, 0);
            _index++;
            return data.Length;
        }
    }
}
