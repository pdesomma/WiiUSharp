namespace WiiUSharp.Nus;

/// <summary>
/// Packs a code/content/meta folder into an installable title.
/// </summary>
public sealed class NusPacker
{
    private readonly CommonKey _commonKey;

    /// <summary>
    /// Creates a new instance of the <see cref="NusPacker"/> class.
    /// </summary>
    /// <param name="commonKey">Wraps the title key in the ticket.</param>
    public NusPacker(CommonKey commonKey)
    {
        _commonKey = commonKey;
    }

    /// <summary>
    /// Writes the TMD, ticket, certificate and every content.
    /// </summary>
    /// <param name="titleDirectory">Folder holding code, content and meta.</param>
    /// <param name="outputDirectory">Folder to write into; created if missing.</param>
    /// <param name="titleKey">Key the contents are encrypted with.</param>
    /// <param name="info">Title values; read from code/app.xml when null.</param>
    /// <param name="rules">Content rules; <see cref="ContentRules.Common(TitleInfo)"/> when null.</param>
    /// <param name="certificateChain">title.cert bytes; the stub when null.</param>
    /// <param name="progress">Receives one line per content.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <exception cref="DirectoryNotFoundException">A required folder is missing.</exception>
    /// <exception cref="InvalidDataException">A file matched no rule, or a folder is empty.</exception>
    public NusPackage Pack(string titleDirectory, string outputDirectory, TitleKey titleKey, TitleInfo? info = null, IReadOnlyList<ContentRule>? rules = null, byte[]? certificateChain = null, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(titleDirectory))
            throw new ArgumentException("Title directory is required.", nameof(titleDirectory));
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("Output directory is required.", nameof(outputDirectory));
        foreach (var folder in new[] { "code", "content", "meta" })
            if (!Directory.Exists(Path.Combine(titleDirectory, folder)))
                throw new DirectoryNotFoundException($"{titleDirectory} has no {folder} folder.");
        if (certificateChain is not null && certificateChain.Length != NusFormat.CertificateChainSize)
            throw new ArgumentException($"Certificate chain must be {NusFormat.CertificateChainSize} bytes.", nameof(certificateChain));

        info ??= TitleInfo.FromAppXml(Path.Combine(titleDirectory, "code", "app.xml"));
        rules ??= ContentRules.Common(info);
        var layout = TitleLayout.Build(titleDirectory, rules);
        Directory.CreateDirectory(outputDirectory);

        var records = new ContentRecord?[layout.Contents.Count];
        using (var encryptor = new ContentEncryptor(titleKey))
        {
            foreach (var content in layout.Contents.Where(c => !c.IsFst))
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report($"Packing content {content.Index:X8} ({content.Files.Count} files)");
                records[content.Index] = Encrypt(encryptor, content, outputDirectory, cancellationToken);
            }

            progress?.Report("Packing the FST");
            var fst = layout.BuildFst(records);
            using var output = File.Create(Path.Combine(outputDirectory, NusFormat.ContentFileName(0)));
            records[0] = encryptor.EncryptPlain(0, new MemoryStream(fst), output, ContentType.Content | ContentType.Encrypted, cancellationToken);
        }

        var contents = records.Select(r => r!).ToArray();
        File.WriteAllBytes(Path.Combine(outputDirectory, NusFormat.TmdFileName), Tmd.Build(info, contents));
        File.WriteAllBytes(Path.Combine(outputDirectory, NusFormat.TicketFileName), Ticket.Build(info.TitleId, titleKey, _commonKey));
        File.WriteAllBytes(Path.Combine(outputDirectory, NusFormat.CertificateFileName), certificateChain ?? CertificateChain.Stub());
        return new NusPackage(outputDirectory, info, contents);
    }

    private static ContentRecord Encrypt(ContentEncryptor encryptor, ContentPlan content, string outputDirectory, CancellationToken cancellationToken)
    {
        using var output = File.Create(Path.Combine(outputDirectory, NusFormat.ContentFileName(content.Index)));
        if (!content.Details.Hashed)
        {
            using var plain = content.OpenPlain();
            return encryptor.EncryptPlain(content.Index, plain, output, content.Details.Type, cancellationToken);
        }

        using var h3 = File.Create(Path.Combine(outputDirectory, NusFormat.HashFileName(content.Index)));
        return encryptor.EncryptHashed(content.Index, content.OpenPlain, output, h3, content.Details.Type, cancellationToken);
    }
}
