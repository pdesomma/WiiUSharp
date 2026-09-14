namespace WiiUSharp.Nfs;

/// <summary>
/// Packs, encrypts and splits a payload into hif_*.nfs files in one pass.
/// </summary>
public sealed class NfsWriter
{
    private readonly NfsKey _key;

    /// <summary>
    /// Creates a new instance of the <see cref="NfsWriter"/> class.
    /// </summary>
    /// <param name="key">Container key.</param>
    public NfsWriter(NfsKey key)
    {
        _key = key;
    }

    /// <summary>
    /// Writes the payload using the standard three-part layout.
    /// </summary>
    /// <param name="payload">Disc image with decrypted partitions.</param>
    /// <param name="data">Game partition range.</param>
    /// <param name="directory">Content directory; existing hif_*.nfs files are replaced.</param>
    /// <param name="progress">Packed bytes written so far.</param>
    /// <param name="cancellationToken">Cancels between sectors.</param>
    /// <returns>Files written, in order.</returns>
    public IReadOnlyList<string> Write(Stream payload, DiscDataSpan data, string directory, IProgress<long>? progress = null, CancellationToken cancellationToken = default) =>
        Write(payload, NfsHeader.ForDisc(data), directory, progress, cancellationToken);

    /// <summary>
    /// Writes the payload using an explicit part layout.
    /// </summary>
    /// <param name="payload">Disc image with decrypted partitions; read forward only.</param>
    /// <param name="header">Parts to store.</param>
    /// <param name="directory">Content directory; existing hif_*.nfs files are replaced.</param>
    /// <param name="progress">Packed bytes written so far.</param>
    /// <param name="cancellationToken">Cancels between sectors.</param>
    /// <returns>Files written, in order.</returns>
    public IReadOnlyList<string> Write(Stream payload, NfsHeader header, string directory, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
    {
        if (payload is null)
            throw new ArgumentNullException(nameof(payload));
        if (header is null)
            throw new ArgumentNullException(nameof(header));

        Directory.CreateDirectory(directory);
        foreach (var stale in Directory.GetFiles(directory, "hif_*.nfs"))
            File.Delete(stale);

        using var cipher = new NfsCipher(_key);
        using var sink = new SplitFileSink(directory);
        var headerBytes = header.ToBytes();
        sink.Write(headerBytes, 0, headerBytes.Length);

        var sector = new byte[NfsFormat.SectorSize];
        long payloadPosition = 0;
        long packedSector = 0;
        long written = 0;
        foreach (var part in header.Parts)
        {
            SkipTo(payload, ref payloadPosition, part.StartOffset, sector);
            for (uint i = 0; i < part.SectorCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = ReadFully(payload, sector);
                if (read < sector.Length)
                    Array.Clear(sector, read, sector.Length - read);
                payloadPosition += read;

                cipher.Encrypt(packedSector++, sector);
                sink.Write(sector, 0, sector.Length);
                written += sector.Length;
                progress?.Report(written);
            }
        }

        return sink.Paths;
    }

    private static int ReadFully(Stream stream, byte[] buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var n = stream.Read(buffer, total, buffer.Length - total);
            if (n == 0)
                break;
            total += n;
        }
        return total;
    }

    private static void SkipTo(Stream payload, ref long position, long target, byte[] scratch)
    {
        if (target < position)
            throw new ArgumentException("Parts must be ascending.", nameof(payload));

        if (payload.CanSeek)
        {
            payload.Position = target;
            position = target;
            return;
        }

        while (position < target)
        {
            var n = payload.Read(scratch, 0, (int)Math.Min(scratch.Length, target - position));
            if (n == 0)
                break;
            position += n;
        }
    }
}
