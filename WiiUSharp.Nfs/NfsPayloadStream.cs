namespace WiiUSharp.Nfs;

/// <summary>
/// Read-only view of the decrypted, gap-expanded payload.
/// </summary>
public sealed class NfsPayloadStream : Stream
{
    private readonly NfsCipher _cipher;
    private readonly NfsHeader _header;
    private readonly long[] _packedStarts;
    private readonly byte[] _sector = new byte[NfsFormat.SectorSize];
    private readonly SplitFileSource _source;
    private long _cachedSector = -1;
    private int _cachedSectorLength;
    private long _position;

    internal NfsPayloadStream(NfsHeader header, NfsKey key, IReadOnlyList<string> files)
    {
        _header = header;
        _cipher = new NfsCipher(key);
        _source = new SplitFileSource(files);
        if (_source.Length < header.PackedLength)
            throw new FormatException($"Container holds {_source.Length} packed bytes but the header describes {header.PackedLength}.");

        _packedStarts = new long[header.Parts.Count];
        long packed = 0;
        for (var i = 0; i < header.Parts.Count; i++)
        {
            _packedStarts[i] = packed;
            packed += header.Parts[i].Length;
        }
    }

    /// <inheritdoc/>
    public override bool CanRead => true;
    /// <inheritdoc/>
    public override bool CanSeek => true;
    /// <inheritdoc/>
    public override bool CanWrite => false;
    /// <inheritdoc/>
    public override long Length => _header.PayloadLength;
    /// <inheritdoc/>
    public override long Position
    {
        get => _position;
        set => _position = value < 0 ? throw new ArgumentOutOfRangeException(nameof(value)) : value;
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (buffer is null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        var total = 0;
        while (count > 0 && _position < Length)
        {
            var part = PartIndexAt(_position);
            int copied;
            if (part < 0)
            {
                var gapEnd = NextPartStart(_position);
                copied = (int)Math.Min(count, gapEnd - _position);
                Array.Clear(buffer, offset, copied);
            }
            else
            {
                var packedOffset = _packedStarts[part] + (_position - _header.Parts[part].StartOffset);
                var sectorIndex = packedOffset / NfsFormat.SectorSize;
                var inSector = (int)(packedOffset % NfsFormat.SectorSize);
                LoadSector(sectorIndex);
                copied = (int)Math.Min(Math.Min(count, _cachedSectorLength - inSector), _header.Parts[part].EndOffset - _position);
                if (copied <= 0)
                    break;
                Array.Copy(_sector, inSector, buffer, offset, copied);
            }

            _position += copied;
            offset += copied;
            count -= copied;
            total += copied;
        }
        return total;
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        var target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
        Position = target;
        return target;
    }

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _source.Dispose();
            _cipher.Dispose();
        }
        base.Dispose(disposing);
    }

    private void LoadSector(long sectorIndex)
    {
        if (sectorIndex == _cachedSector)
            return;

        var read = _source.Read(sectorIndex * NfsFormat.SectorSize, _sector, 0, NfsFormat.SectorSize);
        var usable = read - read % 16;
        if (usable == 0)
            throw new EndOfStreamException("Container ended inside a sector.");

        if (usable == NfsFormat.SectorSize)
        {
            _cipher.Decrypt(sectorIndex, _sector);
        }
        else
        {
            var partial = new byte[usable];
            Array.Copy(_sector, partial, usable);
            _cipher.Decrypt(sectorIndex, partial);
            Array.Copy(partial, _sector, usable);
        }

        _cachedSector = sectorIndex;
        _cachedSectorLength = usable;
    }

    private long NextPartStart(long position)
    {
        foreach (var part in _header.Parts)
            if (part.StartOffset > position)
                return part.StartOffset;
        return Length;
    }

    private int PartIndexAt(long position)
    {
        for (var i = 0; i < _header.Parts.Count; i++)
            if (position >= _header.Parts[i].StartOffset && position < _header.Parts[i].EndOffset)
                return i;
        return -1;
    }
}
