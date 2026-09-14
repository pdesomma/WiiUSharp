namespace WiiUSharp.Nus;

/// <summary>
/// Forward-only view of a list of files, each zero-padded to the content alignment.
/// </summary>
internal sealed class ConcatenatedStream : Stream
{
    private readonly IReadOnlyList<FstEntry> _files;
    private FileStream? _current;
    private long _currentRemaining;
    private long _padRemaining;
    private int _next;
    private long _position;

    public ConcatenatedStream(IReadOnlyList<FstEntry> files)
    {
        _files = files;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _files.Sum(f => ContentPlan.Align(f.Length));
    public override long Position { get => _position; set => throw new NotSupportedException(); }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var total = 0;
        while (count > 0)
        {
            if (_currentRemaining > 0)
            {
                var read = _current!.Read(buffer, offset, (int)Math.Min(count, _currentRemaining));
                if (read == 0)
                    throw new EndOfStreamException($"{_current.Name} is shorter than when it was scanned.");
                _currentRemaining -= read;
                Advance(ref offset, ref count, ref total, read);
            }
            else if (_padRemaining > 0)
            {
                var pad = (int)Math.Min(count, _padRemaining);
                Array.Clear(buffer, offset, pad);
                _padRemaining -= pad;
                Advance(ref offset, ref count, ref total, pad);
            }
            else if (!OpenNext())
            {
                break;
            }
        }
        _position += total;
        return total;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _current?.Dispose();
        base.Dispose(disposing);
    }

    private static void Advance(ref int offset, ref int count, ref int total, int by)
    {
        offset += by;
        count -= by;
        total += by;
    }

    private bool OpenNext()
    {
        _current?.Dispose();
        _current = null;
        if (_next >= _files.Count)
            return false;

        var file = _files[_next++];
        _current = new FileStream(file.Path!, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
        _currentRemaining = file.Length;
        _padRemaining = ContentPlan.Align(file.Length) - file.Length;
        return true;
    }
}
