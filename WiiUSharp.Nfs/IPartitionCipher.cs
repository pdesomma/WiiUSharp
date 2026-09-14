namespace WiiUSharp.Nfs;

/// <summary>
/// Converts Wii disc partition data between the encrypted ISO form and the decrypted form the container stores.
/// </summary>
public interface IPartitionCipher
{
    /// <summary>
    /// Copies an ISO to a payload, decrypting game partition data.
    /// </summary>
    /// <param name="iso">Wii disc image.</param>
    /// <param name="payload">Destination.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>Where the game partitions sit.</returns>
    DiscDataSpan Decrypt(Stream iso, Stream payload, CancellationToken cancellationToken);

    /// <summary>
    /// Copies a payload to an ISO, encrypting game partition data.
    /// </summary>
    /// <param name="payload">Disc image with decrypted partitions.</param>
    /// <param name="iso">Destination.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    void Encrypt(Stream payload, Stream iso, CancellationToken cancellationToken);
}
