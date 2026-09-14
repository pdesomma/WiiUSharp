namespace WiiUSharp.Nus;

/// <summary>
/// How the contents a rule makes are built and what their FST entries carry.
/// </summary>
/// <param name="Hashed">Use the H0–H3 hashed layout.</param>
/// <param name="GroupId">Group ID written to the FST content header.</param>
/// <param name="ParentTitleId">Parent title ID written to the FST content header.</param>
/// <param name="EntryFlags">Flags written to every FST entry in the content.</param>
public sealed record ContentDetails(bool Hashed, ushort GroupId, ulong ParentTitleId, ushort EntryFlags)
{
    /// <summary>
    /// Type bits for a content built this way.
    /// </summary>
    public ContentType Type => ContentType.Content | ContentType.Encrypted | (Hashed ? ContentType.Hashed : 0);
}
