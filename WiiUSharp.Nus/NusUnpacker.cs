using System.Security.Cryptography;

namespace WiiUSharp.Nus;

/// <summary>
/// Turns a downloaded package (title.tmd, title.tik, *.app, *.h3) back into a code/content/meta folder.
/// </summary>
public sealed class NusUnpacker
{
    private const int CopyBufferSize = 1 << 20;

    private readonly CommonKey _commonKey;

    /// <summary>
    /// Creates a new instance of the <see cref="NusUnpacker"/> class.
    /// </summary>
    /// <param name="commonKey">Unwraps the title key in the ticket.</param>
    public NusUnpacker(CommonKey commonKey)
    {
        _commonKey = commonKey;
    }

    /// <summary>
    /// True when the ticket's title key, unwrapped with the common key, decrypts content 0 to an FST.
    /// </summary>
    /// <param name="packageDirectory">Folder holding title.tmd, title.tik and 00000000.app at least.</param>
    public bool KeysMatch(string packageDirectory) => KeysMatch(packageDirectory, ReadTitleKey(packageDirectory));

    /// <summary>
    /// True when the title key decrypts content 0 to an FST.
    /// </summary>
    /// <param name="packageDirectory">Folder holding title.tmd and 00000000.app at least.</param>
    /// <param name="titleKey">Plain title key.</param>
    public static bool KeysMatch(string packageDirectory, TitleKey titleKey)
    {
        RequireDirectory(packageDirectory);
        var tmd = ReadTmd(packageDirectory);
        var fst = tmd.Contents[0];
        using var decryptor = new ContentDecryptor(titleKey);
        using var encrypted = OpenContent(packageDirectory, fst);
        return Fst.HasMagic(decryptor.PeekPlain(fst, encrypted));
    }

    /// <summary>
    /// Unpacks with the title key the ticket carries.
    /// </summary>
    /// <param name="packageDirectory">Folder holding the package.</param>
    /// <param name="outputDirectory">Where code, content and meta are written.</param>
    /// <param name="progress">One line per content.</param>
    /// <param name="cancellationToken">Stops between files.</param>
    /// <exception cref="InvalidDataException">The keys are wrong or a content fails its hash.</exception>
    public NusPackage Unpack(string packageDirectory, string outputDirectory, IProgress<string>? progress = null, CancellationToken cancellationToken = default) =>
        Unpack(packageDirectory, outputDirectory, ReadTitleKey(packageDirectory), progress, cancellationToken);

    /// <summary>
    /// Unpacks with an explicit title key; the ticket is not read.
    /// </summary>
    /// <param name="packageDirectory">Folder holding the package.</param>
    /// <param name="outputDirectory">Where code, content and meta are written.</param>
    /// <param name="titleKey">Plain title key.</param>
    /// <param name="progress">One line per content.</param>
    /// <param name="cancellationToken">Stops between files.</param>
    /// <exception cref="InvalidDataException">The key is wrong or a content fails its hash.</exception>
    public static NusPackage Unpack(string packageDirectory, string outputDirectory, TitleKey titleKey, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        RequireDirectory(packageDirectory);
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("Output directory is required.", nameof(outputDirectory));

        var tmd = ReadTmd(packageDirectory);
        using var decryptor = new ContentDecryptor(titleKey);

        progress?.Report("Reading the FST");
        var fst = ReadFst(packageDirectory, tmd.Contents[0], decryptor);
        var byContent = fst.Files.GroupBy(f => f.ContentIndex).ToDictionary(g => g.Key, g => g.OrderBy(f => f.Offset).ToArray());

        Directory.CreateDirectory(outputDirectory);
        foreach (var record in tmd.Contents.Skip(1))
        {
            if (!byContent.TryGetValue(record.Index, out var files))
                continue;

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report($"Unpacking content {record.Id:X8} ({files.Length} files)");
            Extract(packageDirectory, outputDirectory, record, files, decryptor, cancellationToken);
        }
        return new NusPackage(packageDirectory, tmd.Title, tmd.Contents);
    }

    private static void Copy(Stream plain, Stream output, long count, byte[] buffer, string path)
    {
        while (count > 0)
        {
            var read = plain.Read(buffer, 0, (int)Math.Min(buffer.Length, count));
            if (read == 0)
                throw new InvalidDataException($"{path} runs past the end of its content.");
            output.Write(buffer, 0, read);
            count -= read;
        }
    }

    private static void Extract(string packageDirectory, string outputDirectory, ContentRecord record, FstFile[] files, ContentDecryptor decryptor, CancellationToken cancellationToken)
    {
        var h3 = record.IsHashed ? ReadH3(packageDirectory, record) : null;
        using var encrypted = OpenContent(packageDirectory, record);
        using var plain = decryptor.OpenPlain(record, encrypted, h3);
        var buffer = new byte[CopyBufferSize];
        long position = 0;
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (file.Offset < position)
                throw new InvalidDataException($"{file.Path} overlaps the file before it in content {record.Id:X8}.");
            Skip(plain, file.Offset - position, buffer);

            var path = Path.Combine(outputDirectory, file.Path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize))
                Copy(plain, output, file.Length, buffer, file.Path);
            position = file.Offset + file.Length;
        }
        Skip(plain, long.MaxValue, buffer);
    }

    private static Stream OpenContent(string packageDirectory, ContentRecord record)
    {
        var path = Path.Combine(packageDirectory, NusFormat.ContentFileName(record.Id));
        if (!File.Exists(path))
            throw new FileNotFoundException($"Content {record.Id:X8} is missing.", path);
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize);
    }

    private static Fst ReadFst(string packageDirectory, ContentRecord record, ContentDecryptor decryptor)
    {
        using var encrypted = OpenContent(packageDirectory, record);
        if (!Fst.HasMagic(decryptor.PeekPlain(record, encrypted)))
            throw new InvalidDataException("Content 0 is not an FST; the title key or common key is wrong.");

        encrypted.Position = 0;
        using var plain = decryptor.OpenPlain(record, encrypted, null);
        using var memory = new MemoryStream();
        plain.CopyTo(memory);
        return Fst.Parse(memory.ToArray());
    }

    private static byte[] ReadH3(string packageDirectory, ContentRecord record)
    {
        var path = Path.Combine(packageDirectory, NusFormat.HashFileName(record.Id));
        if (!File.Exists(path))
            throw new FileNotFoundException($"H3 table of content {record.Id:X8} is missing.", path);

        var h3 = File.ReadAllBytes(path);
        using var sha = SHA1.Create();
        if (!sha.ComputeHash(h3).SequenceEqual(record.Hash))
            throw new InvalidDataException($"H3 table of content {record.Id:X8} does not match the TMD.");
        return h3;
    }

    private TitleKey ReadTitleKey(string packageDirectory)
    {
        RequireDirectory(packageDirectory);
        var path = Path.Combine(packageDirectory, NusFormat.TicketFileName);
        if (!File.Exists(path))
            throw new FileNotFoundException("Ticket is missing.", path);

        var ticket = Ticket.Parse(File.ReadAllBytes(path));
        return ticket.TitleKey.Decrypt(ticket.TitleId, _commonKey);
    }

    private static TmdInfo ReadTmd(string packageDirectory)
    {
        var path = Path.Combine(packageDirectory, NusFormat.TmdFileName);
        if (!File.Exists(path))
            throw new FileNotFoundException("TMD is missing.", path);
        return Tmd.Parse(File.ReadAllBytes(path));
    }

    private static void RequireDirectory(string packageDirectory)
    {
        if (string.IsNullOrWhiteSpace(packageDirectory))
            throw new ArgumentException("Package directory is required.", nameof(packageDirectory));
        if (!Directory.Exists(packageDirectory))
            throw new DirectoryNotFoundException($"{packageDirectory} does not exist.");
    }

    private static void Skip(Stream plain, long count, byte[] buffer)
    {
        while (count > 0)
        {
            var read = plain.Read(buffer, 0, (int)Math.Min(buffer.Length, count));
            if (read == 0)
                return;
            count -= read;
        }
    }
}
