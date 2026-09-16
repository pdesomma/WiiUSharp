using System.Globalization;
using System.Text.RegularExpressions;

namespace WiiUSharp.Wud;

/// <summary>
/// Opens a disc dump as one seekable stream, whatever shape it was dumped in: a single .wud, a .wux, or the game_part1.wud, game_part2.wud… series wudump writes.
/// </summary>
public static class WudImage
{
    private static readonly Regex PartName = new(@"^(?<stem>.*_part)(?<number>\d+)(?<extension>\.wud)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Opens the image; the returned stream owns every file behind it.
    /// </summary>
    /// <param name="path">A .wud, a .wux, or any part of a split .wud.</param>
    /// <exception cref="NotSupportedException">Any other extension.</exception>
    public static Stream Open(string path)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));

        var extension = Path.GetExtension(path);
        if (string.Equals(extension, ".wux", StringComparison.OrdinalIgnoreCase))
            return WuxStream.Open(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
        if (!string.Equals(extension, ".wud", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Only .wud and .wux images are supported.");

        var parts = Parts(path);
        if (parts.Count == 1)
        {
            var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            // a .wud renamed from a .wux still opens
            return WuxStream.IsWux(file) ? WuxStream.Open(file) : file;
        }
        return new ConcatenatedFileStream(parts);
    }

    /// <summary>
    /// Every file of a split dump in order, or just <paramref name="path"/> when it is not one.
    /// </summary>
    /// <param name="path">Any part of the dump.</param>
    public static IReadOnlyList<string> Parts(string path)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));

        var match = PartName.Match(Path.GetFileName(path));
        if (!match.Success)
            return new[] { path };

        var folder = Path.GetDirectoryName(path) ?? "";
        var stem = match.Groups["stem"].Value;
        var extension = match.Groups["extension"].Value;
        var parts = new List<string>();
        for (var number = 1; ; number++)
        {
            var candidate = Path.Combine(folder, stem + number.ToString(CultureInfo.InvariantCulture) + extension);
            if (!File.Exists(candidate))
                break;
            parts.Add(candidate);
        }
        return parts.Count == 0 ? new[] { path } : parts;
    }

    /// <summary>
    /// Several files read back to back as one.
    /// </summary>
    private sealed class ConcatenatedFileStream : Stream
    {
        private readonly long[] _ends;
        private readonly IReadOnlyList<string> _paths;
        private readonly FileStream?[] _streams;
        private long _position;

        /// <summary>
        /// Creates a new instance of the <see cref="ConcatenatedFileStream"/> class.
        /// </summary>
        /// <param name="paths">Files in order.</param>
        public ConcatenatedFileStream(IReadOnlyList<string> paths)
        {
            _paths = paths;
            _streams = new FileStream?[paths.Count];
            _ends = new long[paths.Count];
            long total = 0;
            for (var i = 0; i < paths.Count; i++)
            {
                total += new FileInfo(paths[i]).Length;
                _ends[i] = total;
            }
            Length = total;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length { get; }
        public override long Position
        {
            get => _position;
            set => _position = value < 0 ? throw new ArgumentOutOfRangeException(nameof(value)) : value;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer is null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            var total = 0;
            while (count > 0 && _position < Length)
            {
                var file = 0;
                while (_position >= _ends[file])
                    file++;
                var start = file == 0 ? 0 : _ends[file - 1];
                var stream = _streams[file] ??= new FileStream(_paths[file], FileMode.Open, FileAccess.Read, FileShare.Read);
                stream.Position = _position - start;
                var read = stream.Read(buffer, offset, (int)Math.Min(count, _ends[file] - _position));
                if (read == 0)
                    throw new EndOfStreamException("A part of the dump is shorter than it was.");
                _position += read;
                offset += read;
                count -= read;
                total += read;
            }
            return total;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            Position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => Length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
            return _position;
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var stream in _streams)
                    stream?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
