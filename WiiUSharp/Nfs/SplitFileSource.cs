namespace WiiUSharp.Nfs;

/// <summary>
/// Reads the packed payload as one range across the split files, skipping the header.
/// </summary>
internal sealed class SplitFileSource : IDisposable
{
    private readonly long[] _ends;
    private readonly IReadOnlyList<string> _paths;
    private readonly FileStream?[] _streams;

    public SplitFileSource(IReadOnlyList<string> paths)
    {
        _paths = paths;
        _streams = new FileStream?[paths.Count];
        _ends = new long[paths.Count];

        long total = 0;
        for (var i = 0; i < paths.Count; i++)
        {
            var length = new FileInfo(paths[i]).Length;
            total += i == 0 ? length - NfsFormat.HeaderSize : length;
            _ends[i] = total;
        }
        Length = total;
    }

    public long Length { get; }

    public void Dispose()
    {
        foreach (var stream in _streams)
            stream?.Dispose();
    }

    public int Read(long offset, byte[] buffer, int bufferOffset, int count)
    {
        var total = 0;
        while (count > 0 && offset < Length)
        {
            var file = FileIndex(offset);
            var fileStart = file == 0 ? 0 : _ends[file - 1];
            var inFile = offset - fileStart + (file == 0 ? NfsFormat.HeaderSize : 0);
            var available = (int)Math.Min(count, _ends[file] - offset);

            var stream = _streams[file] ??= new FileStream(_paths[file], FileMode.Open, FileAccess.Read, FileShare.Read);
            stream.Position = inFile;
            var read = stream.Read(buffer, bufferOffset, available);
            if (read == 0)
                break;

            total += read;
            offset += read;
            bufferOffset += read;
            count -= read;
        }
        return total;
    }

    private int FileIndex(long offset)
    {
        for (var i = 0; i < _ends.Length; i++)
            if (offset < _ends[i])
                return i;
        throw new ArgumentOutOfRangeException(nameof(offset));
    }
}
