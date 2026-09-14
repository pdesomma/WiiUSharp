namespace WiiUSharp.Nfs;

/// <summary>
/// Writes a byte stream into hif_*.nfs files of at most a fixed size each.
/// </summary>
internal sealed class SplitFileSink : IDisposable
{
    private readonly string _directory;
    private readonly long _fileSize;
    private readonly List<string> _paths = new();
    private FileStream? _current;
    private long _inCurrent;

    public SplitFileSink(string directory, long fileSize = NfsFormat.FileSize)
    {
        _directory = directory;
        _fileSize = fileSize;
    }

    public IReadOnlyList<string> Paths => _paths;

    public void Dispose() => _current?.Dispose();

    public void Write(byte[] buffer, int offset, int count)
    {
        while (count > 0)
        {
            if (_current is null || _inCurrent == _fileSize)
                OpenNext();

            var chunk = (int)Math.Min(count, _fileSize - _inCurrent);
            _current!.Write(buffer, offset, chunk);
            _inCurrent += chunk;
            offset += chunk;
            count -= chunk;
        }
    }

    private void OpenNext()
    {
        _current?.Dispose();
        var path = Path.Combine(_directory, NfsFormat.FileName(_paths.Count));
        _current = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        _inCurrent = 0;
        _paths.Add(path);
    }
}
