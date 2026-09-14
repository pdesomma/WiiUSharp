namespace WiiUSharp.Nus;

/// <summary>
/// A content before packing: which files go in it and how.
/// </summary>
internal sealed class ContentPlan
{
    public ContentPlan(int index, ContentDetails details, bool isFst = false)
    {
        Index = index;
        Details = details;
        IsFst = isFst;
    }

    public ContentDetails Details { get; }
    public List<FstEntry> Files { get; } = new();
    public int Index { get; set; }
    public bool IsFst { get; }
    /// <summary>
    /// Plaintext length: every file padded to the content alignment.
    /// </summary>
    public long PlainLength { get; private set; }

    /// <summary>
    /// Gives each file its offset in the content.
    /// </summary>
    public void Layout()
    {
        long offset = 0;
        foreach (var file in Files)
        {
            file.Offset = offset;
            offset += Align(file.Length);
        }
        PlainLength = offset;
    }

    /// <summary>
    /// Read-only plaintext of the content: the files back to back with their padding.
    /// </summary>
    public Stream OpenPlain() => new ConcatenatedStream(Files);

    public static long Align(long length) =>
        (length + NusFormat.ContentAlignment - 1) / NusFormat.ContentAlignment * NusFormat.ContentAlignment;
}
