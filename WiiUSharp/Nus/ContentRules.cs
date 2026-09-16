namespace WiiUSharp.Nus;

/// <summary>
/// The standard rule table.
/// </summary>
public static class ContentRules
{
    /// <summary>
    /// Entry flags for files under code.
    /// </summary>
    public const ushort CodeFlags = 0x0000;
    /// <summary>
    /// Group ID for code contents.
    /// </summary>
    public const ushort CodeGroup = 0x0000;
    /// <summary>
    /// Entry flags for files under content.
    /// </summary>
    public const ushort ContentFlags = 0x0400;
    /// <summary>
    /// Entry flags for files under meta.
    /// </summary>
    public const ushort MetaFlags = 0x0040;
    /// <summary>
    /// Group ID for meta contents.
    /// </summary>
    public const ushort MetaGroup = 0x0400;

    /// <summary>
    /// The NUSPacker rule table for a game title.
    /// </summary>
    /// <param name="groupId">Group ID for the content folder.</param>
    /// <param name="parentTitleId">Parent title ID for the content folder.</param>
    public static IReadOnlyList<ContentRule> Common(ushort groupId, ulong parentTitleId)
    {
        var code = new ContentDetails(false, CodeGroup, 0, CodeFlags);
        var meta = new ContentDetails(true, MetaGroup, 0, MetaFlags);
        return new[]
        {
            new ContentRule("/code/app.xml", code),
            new ContentRule("/code/cos.xml", code),
            new ContentRule("/meta/meta.xml", meta),
            new ContentRule("/meta/.*[^.xml)]+", meta),
            new ContentRule("/meta/bootMovie.h264", meta),
            new ContentRule("/meta/bootLogoTex.tga", meta),
            new ContentRule("/meta/Manual.bfma", meta),
            new ContentRule("/meta/.*.jpg", meta),
            new ContentRule("/code/.*(.rpx|.rpl)", code, contentPerMatch: true),
            new ContentRule("/code/preload.txt", new ContentDetails(true, CodeGroup, 0, CodeFlags)),
            new ContentRule("/code/fw.img", code),
            new ContentRule("/code/fw.tmd", code),
            new ContentRule("/code/htk.bin", code),
            new ContentRule("/code/rvlt.tik", code),
            new ContentRule("/code/rvlt.tmd", code),
            new ContentRule("/content/.*", new ContentDetails(true, groupId, parentTitleId, ContentFlags)),
        };
    }

    /// <summary>
    /// <see cref="Common(ushort, ulong)"/> with the group and parent taken from the title.
    /// </summary>
    /// <param name="info">Title being packed.</param>
    public static IReadOnlyList<ContentRule> Common(TitleInfo info)
    {
        if (info is null)
            throw new ArgumentNullException(nameof(info));

        return Common(info.GroupId, info.ParentTitleId);
    }
}
