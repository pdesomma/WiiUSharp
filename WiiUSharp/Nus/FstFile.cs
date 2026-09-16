namespace WiiUSharp.Nus;

/// <summary>
/// One file the FST places inside a content.
/// </summary>
/// <param name="Path">Title path with forward slashes, e.g. /code/app.xml.</param>
/// <param name="ContentIndex">Content the bytes live in.</param>
/// <param name="Offset">Byte offset in the decrypted content.</param>
/// <param name="Length">File length.</param>
/// <param name="Flags">Entry flags as stored.</param>
public sealed record FstFile(string Path, int ContentIndex, long Offset, long Length, ushort Flags);
