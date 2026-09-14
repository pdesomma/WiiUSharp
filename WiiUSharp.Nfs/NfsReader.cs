namespace WiiUSharp.Nfs;

/// <summary>
/// Opens a content directory holding hif_*.nfs files.
/// </summary>
public sealed class NfsReader
{
    private readonly NfsKey _key;

    private NfsReader(NfsHeader header, IReadOnlyList<string> files, NfsKey key)
    {
        Header = header;
        Files = files;
        _key = key;
    }

    /// <summary>
    /// Split files in order.
    /// </summary>
    public IReadOnlyList<string> Files { get; }
    /// <summary>
    /// Header from the first file.
    /// </summary>
    public NfsHeader Header { get; }

    /// <summary>
    /// Reads the header and finds the split files.
    /// </summary>
    /// <param name="directory">Directory containing hif_000000.nfs.</param>
    /// <param name="key">Container key.</param>
    /// <exception cref="FileNotFoundException">No hif_000000.nfs.</exception>
    /// <exception cref="FormatException">First file does not start with an NFS header.</exception>
    public static NfsReader Open(string directory, NfsKey key)
    {
        var files = new List<string>();
        for (var i = 0; ; i++)
        {
            var path = Path.Combine(directory, NfsFormat.FileName(i));
            if (!File.Exists(path))
                break;
            files.Add(path);
        }
        if (files.Count == 0)
            throw new FileNotFoundException("No NFS files found.", Path.Combine(directory, NfsFormat.FileName(0)));

        var headerBytes = new byte[NfsFormat.HeaderSize];
        using (var first = File.OpenRead(files[0]))
        {
            var read = 0;
            while (read < headerBytes.Length)
            {
                var n = first.Read(headerBytes, read, headerBytes.Length - read);
                if (n == 0)
                    break;
                read += n;
            }
            if (read < headerBytes.Length)
                throw new FormatException("First file is shorter than the header.");
        }

        return new NfsReader(NfsHeader.Parse(headerBytes), files, key);
    }

    /// <summary>
    /// Decrypted payload with gaps expanded to zeros.
    /// </summary>
    public NfsPayloadStream OpenPayload() => new(Header, _key, Files);
}
