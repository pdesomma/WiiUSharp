namespace WiiUSharp.Nus;

/// <summary>
/// What a TMD says: title values and one record per content.
/// </summary>
public sealed class TmdInfo
{
    /// <summary>
    /// Creates a new instance of the <see cref="TmdInfo"/> class.
    /// </summary>
    /// <param name="title">Title values.</param>
    /// <param name="contents">Records in index order.</param>
    public TmdInfo(TitleInfo title, IReadOnlyList<ContentRecord> contents)
    {
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Contents = contents ?? throw new ArgumentNullException(nameof(contents));
    }

    /// <summary>
    /// Records in index order.
    /// </summary>
    public IReadOnlyList<ContentRecord> Contents { get; }
    /// <summary>
    /// Title values.
    /// </summary>
    public TitleInfo Title { get; }
}
