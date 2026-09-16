namespace WiiUSharp.Nfs;

/// <summary>
/// Byte range of a disc image covering its game partitions.
/// </summary>
/// <param name="Offset">Start of the first game partition.</param>
/// <param name="Length">Bytes to the end of the last game partition.</param>
public readonly record struct DiscDataSpan(long Offset, long Length);
