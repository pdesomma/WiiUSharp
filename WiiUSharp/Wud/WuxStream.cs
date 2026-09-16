namespace WiiUSharp.Wud;

/// <summary>
/// Seekable, read-only view of the disc inside a .wux: identical sectors are stored once and an index maps every disc sector to its copy.
/// </summary>
public sealed class WuxStream : Stream
{
    /// <summary>
    /// "WUX0" as the file stores it.
    /// </summary>
    public const uint Magic0 = 0x30585557;
    /// <summary>
    /// Second magic word.
    /// </summary>
    public const uint Magic1 = 0x1099D02E;
    /// <summary>
    /// Bytes of the file header ahead of the sector index.
    /// </summary>
    public const int HeaderSize = 0x20;

    private readonly Stream _file;
    private readonly uint[] _index;
    private readonly long _dataStart;
    private readonly bool _leaveOpen;
    private readonly long _length;
    private readonly int _sectorSize;
    private long _position;

    private WuxStream(Stream file, bool leaveOpen, int sectorSize, long length, uint[] index, long dataStart)
    {
        _file = file;
        _leaveOpen = leaveOpen;
        _sectorSize = sectorSize;
        _length = length;
        _index = index;
        _dataStart = dataStart;
    }

    /// <inheritdoc/>
    public override bool CanRead => true;
    /// <inheritdoc/>
    public override bool CanSeek => true;
    /// <inheritdoc/>
    public override bool CanWrite => false;
    /// <inheritdoc/>
    public override long Length => _length;
    /// <inheritdoc/>
    public override long Position
    {
        get => _position;
        set => _position = value < 0 ? throw new ArgumentOutOfRangeException(nameof(value)) : value;
    }
    /// <summary>
    /// Bytes per indexed sector.
    /// </summary>
    public int SectorSize => _sectorSize;

    /// <summary>
    /// True when the stream starts with the WUX magic; leaves the position at 0.
    /// </summary>
    /// <param name="stream">Seekable stream.</param>
    public static bool IsWux(Stream stream)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));

        var head = new byte[8];
        stream.Position = 0;
        var read = Fill(stream, head, 0, head.Length);
        stream.Position = 0;
        return read == head.Length && LittleEndian(head, 0) == Magic0 && LittleEndian(head, 4) == Magic1;
    }

    /// <summary>
    /// Opens the view over a .wux stream.
    /// </summary>
    /// <param name="file">Seekable .wux; the view owns it unless <paramref name="leaveOpen"/>.</param>
    /// <param name="leaveOpen">Keep <paramref name="file"/> open when the view is disposed.</param>
    /// <exception cref="InvalidDataException">Not a WUX, or an index that does not fit.</exception>
    public static WuxStream Open(Stream file, bool leaveOpen = false)
    {
        if (file is null)
            throw new ArgumentNullException(nameof(file));
        if (!file.CanSeek)
            throw new ArgumentException("WUX must be seekable.", nameof(file));

        var header = new byte[HeaderSize];
        file.Position = 0;
        if (Fill(file, header, 0, header.Length) != header.Length || LittleEndian(header, 0) != Magic0 || LittleEndian(header, 4) != Magic1)
            throw new InvalidDataException("Not a WUX image.");

        var sectorSize = checked((int)LittleEndian(header, 8));
        var length = (long)LittleEndian(header, 16) | (long)LittleEndian(header, 20) << 32;
        if (sectorSize < 0x100 || sectorSize > 0x10000000 || length < 0)
            throw new InvalidDataException("WUX header holds an impossible sector size or length.");

        var count = checked((int)((length + sectorSize - 1) / sectorSize));
        var table = new byte[count * 4];
        if (Fill(file, table, 0, table.Length) != table.Length)
            throw new InvalidDataException("WUX sector index is cut short.");
        var index = new uint[count];
        for (var i = 0; i < count; i++)
            index[i] = LittleEndian(table, i * 4);

        var dataStart = (HeaderSize + (long)table.Length + sectorSize - 1) / sectorSize * sectorSize;
        return new WuxStream(file, leaveOpen, sectorSize, length, index, dataStart);
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
        while (count > 0 && _position < _length)
        {
            var sector = _position / _sectorSize;
            var within = (int)(_position % _sectorSize);
            var chunk = (int)Math.Min(count, Math.Min(_sectorSize - within, _length - _position));
            _file.Position = _dataStart + (long)_index[sector] * _sectorSize + within;
            var read = _file.Read(buffer, offset, chunk);
            if (read == 0)
                throw new EndOfStreamException("WUX ends inside a stored sector.");
            _position += read;
            offset += read;
            count -= read;
            total += read;
        }
        return total;
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
        return _position;
    }

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_leaveOpen)
            _file.Dispose();
        base.Dispose(disposing);
    }

    private static int Fill(Stream stream, byte[] buffer, int offset, int count)
    {
        var total = 0;
        while (total < count)
        {
            var read = stream.Read(buffer, offset + total, count - total);
            if (read == 0)
                break;
            total += read;
        }
        return total;
    }

    private static uint LittleEndian(byte[] bytes, int offset) =>
        (uint)(bytes[offset] | bytes[offset + 1] << 8 | bytes[offset + 2] << 16 | bytes[offset + 3] << 24);
}
