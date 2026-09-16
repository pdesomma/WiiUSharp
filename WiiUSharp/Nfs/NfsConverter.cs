namespace WiiUSharp.Nfs;

/// <summary>
/// Whole-image conversion between an ISO and a content directory.
/// </summary>
public sealed class NfsConverter
{
    /// <summary>
    /// Size an ISO is padded to when it exceeds a single layer.
    /// </summary>
    public const long DualLayerSize = 0x1FB4E0000;
    /// <summary>
    /// Size an ISO is padded to when it fits a single layer.
    /// </summary>
    public const long SingleLayerSize = 0x118240000;

    private readonly IPartitionCipher _partitions;

    /// <summary>
    /// Creates a new instance of the <see cref="NfsConverter"/> class.
    /// </summary>
    /// <param name="partitions">Wii partition crypto.</param>
    public NfsConverter(IPartitionCipher partitions)
    {
        _partitions = partitions ?? throw new ArgumentNullException(nameof(partitions));
    }

    /// <summary>
    /// Writes an ISO into a content directory.
    /// </summary>
    /// <param name="iso">Wii disc image.</param>
    /// <param name="key">Container key.</param>
    /// <param name="contentDirectory">Destination for hif_*.nfs.</param>
    /// <param name="tempDirectory">Holds the decrypted payload while packing.</param>
    /// <param name="progress">Packed bytes written so far.</param>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>Files written, in order.</returns>
    public IReadOnlyList<string> FromIso(Stream iso, NfsKey key, string contentDirectory, string tempDirectory, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(tempDirectory);
        var payloadPath = Path.Combine(tempDirectory, Path.GetRandomFileName());
        try
        {
            DiscDataSpan span;
            using (var payload = new FileStream(payloadPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                span = _partitions.Decrypt(iso, payload, cancellationToken);
                payload.Position = 0;
                return new NfsWriter(key).Write(payload, span, contentDirectory, progress, cancellationToken);
            }
        }
        finally
        {
            File.Delete(payloadPath);
        }
    }

    /// <summary>
    /// Reads a content directory into a full-size ISO.
    /// </summary>
    /// <param name="contentDirectory">Directory holding hif_*.nfs.</param>
    /// <param name="key">Container key.</param>
    /// <param name="iso">Destination; padded to a full layer.</param>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    public void ToIso(string contentDirectory, NfsKey key, Stream iso, CancellationToken cancellationToken = default)
    {
        using var payload = NfsReader.Open(contentDirectory, key).OpenPayload();
        _partitions.Encrypt(payload, iso, cancellationToken);
        PadToLayer(iso, cancellationToken);
    }

    private static void PadToLayer(Stream iso, CancellationToken cancellationToken)
    {
        var target = iso.Position > SingleLayerSize ? DualLayerSize : SingleLayerSize;
        var zeros = new byte[NfsFormat.SectorSize];
        while (iso.Position < target)
        {
            cancellationToken.ThrowIfCancellationRequested();
            iso.Write(zeros, 0, (int)Math.Min(zeros.Length, target - iso.Position));
        }
    }
}
