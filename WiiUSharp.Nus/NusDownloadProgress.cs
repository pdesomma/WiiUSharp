namespace WiiUSharp.Nus;

/// <summary>
/// How far a download is: which file, and how much of it.
/// </summary>
/// <param name="File">File being written, e.g. 00000001.app.</param>
/// <param name="FileNumber">One-based position of the file.</param>
/// <param name="FileCount">Files the package needs.</param>
/// <param name="BytesReceived">Bytes of the file written so far.</param>
/// <param name="BytesTotal">File length when the server said; null otherwise.</param>
public sealed record NusDownloadProgress(string File, int FileNumber, int FileCount, long BytesReceived, long? BytesTotal);
