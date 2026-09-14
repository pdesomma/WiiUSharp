namespace WiiUSharp.Nus;

/// <summary>
/// What <see cref="NusPacker.Pack"/> wrote.
/// </summary>
public sealed class NusPackage
{
    /// <summary>
    /// Creates a new instance of the <see cref="NusPackage"/> class.
    /// </summary>
    /// <param name="outputDirectory">Folder holding the files.</param>
    /// <param name="title">Title values used.</param>
    /// <param name="contents">Content records in index order.</param>
    public NusPackage(string outputDirectory, TitleInfo title, IReadOnlyList<ContentRecord> contents)
    {
        OutputDirectory = outputDirectory ?? throw new ArgumentNullException(nameof(outputDirectory));
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Contents = contents ?? throw new ArgumentNullException(nameof(contents));
    }

    /// <summary>
    /// Content records in index order.
    /// </summary>
    public IReadOnlyList<ContentRecord> Contents { get; }
    /// <summary>
    /// Every file written, TMD, ticket and certificate first.
    /// </summary>
    public IEnumerable<string> Files
    {
        get
        {
            yield return Path.Combine(OutputDirectory, NusFormat.TmdFileName);
            yield return Path.Combine(OutputDirectory, NusFormat.TicketFileName);
            yield return Path.Combine(OutputDirectory, NusFormat.CertificateFileName);
            foreach (var content in Contents)
            {
                yield return Path.Combine(OutputDirectory, NusFormat.ContentFileName(content.Index));
                if (content.IsHashed)
                    yield return Path.Combine(OutputDirectory, NusFormat.HashFileName(content.Index));
            }
        }
    }
    /// <summary>
    /// Folder holding the files.
    /// </summary>
    public string OutputDirectory { get; }
    /// <summary>
    /// Title values used.
    /// </summary>
    public TitleInfo Title { get; }
}
