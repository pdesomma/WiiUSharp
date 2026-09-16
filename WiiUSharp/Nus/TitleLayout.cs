using System.Text;

namespace WiiUSharp.Nus;

/// <summary>
/// The title tree with every file assigned to a content; builds the FST.
/// </summary>
internal sealed class TitleLayout
{
    private const int ContentHeaderSize = 0x20;
    private const int HeaderSize = 0x20;
    private static readonly byte[] Magic = { 0x46, 0x53, 0x54, 0x00 };

    private TitleLayout(FstEntry root, List<ContentPlan> contents)
    {
        Root = root;
        Contents = contents;
    }

    public IReadOnlyList<ContentPlan> Contents { get; }
    public IReadOnlyList<FstEntry> Entries => Root.Traverse().ToArray();
    public FstEntry Root { get; }

    /// <summary>
    /// Scans a title folder and applies the rules in order.
    /// </summary>
    /// <exception cref="InvalidDataException">A file matched no rule, or a folder has nothing in it.</exception>
    public static TitleLayout Build(string directory, IReadOnlyList<ContentRule> rules, long maxContentLength = NusFormat.MaxContentLength)
    {
        var root = Scan(directory);
        var contents = new List<ContentPlan> { new(0, new ContentDetails(false, 0, 0, 0), isFst: true) };
        var files = root.Traverse().Where(e => !e.IsDirectory).ToArray();

        foreach (var rule in rules)
        {
            if (rule.ContentPerMatch)
            {
                foreach (var file in files.Where(f => rule.Matches(f.TitlePath)))
                {
                    var content = NewContent(contents, rule.Details);
                    Assign(file, content, content);
                }
                continue;
            }

            var first = NewContent(contents, rule.Details);
            var current = first;
            long size = 0;
            var matched = false;
            foreach (var file in files.Where(f => rule.Matches(f.TitlePath)))
            {
                if (size > 0 && size + file.Length > maxContentLength)
                {
                    current = NewContent(contents, rule.Details);
                    size = 0;
                }
                size += file.Length;
                Assign(file, current, first);
                matched = true;
            }
            if (!matched)
                contents.Remove(first);
        }

        foreach (var entry in root.Traverse().Where(e => !e.IsRoot && e.Content is null))
            throw new InvalidDataException(entry.IsDirectory
                ? $"Folder {entry.TitlePath} has no files; add one or remove it."
                : $"File {entry.TitlePath} matched no content rule.");

        for (var i = 0; i < contents.Count; i++)
        {
            contents[i].Index = i;
            contents[i].Layout();
        }

        var index = 0;
        var names = 0;
        foreach (var entry in root.Traverse())
        {
            entry.Index = index++;
            entry.NameOffset = names;
            names += Encoding.UTF8.GetByteCount(entry.Name) + 1;
        }
        return new TitleLayout(root, contents);
    }

    /// <summary>
    /// The FST bytes, padded to the content boundary.
    /// </summary>
    /// <param name="records">TMD records of every non-FST content; sizes feed the content headers.</param>
    public byte[] BuildFst(IReadOnlyList<ContentRecord?> records)
    {
        var entries = Entries;
        var strings = new MemoryStream();
        foreach (var entry in entries)
        {
            var name = Encoding.UTF8.GetBytes(entry.Name);
            strings.Write(name, 0, name.Length);
            strings.WriteByte(0);
        }

        var length = HeaderSize + Contents.Count * ContentHeaderSize + entries.Count * FstEntry.Size + strings.Length;
        var padded = (length + NusFormat.ContentPadding - 1) / NusFormat.ContentPadding * NusFormat.ContentPadding;
        var fst = new byte[padded];
        Magic.CopyTo(fst, 0);
        BigEndian.Write(fst, 0x04, 0x20u);
        BigEndian.Write(fst, 0x08, (uint)Contents.Count);

        var offset = HeaderSize;
        long accumulator = 0;
        foreach (var content in Contents)
        {
            WriteContentHeader(fst, offset, content, records[content.Index], ref accumulator);
            offset += ContentHeaderSize;
        }
        foreach (var entry in entries)
        {
            entry.ToBytes().CopyTo(fst, offset);
            offset += FstEntry.Size;
        }
        strings.ToArray().CopyTo(fst, offset);
        return fst;
    }

    private static void Assign(FstEntry file, ContentPlan content, ContentPlan forFolders)
    {
        file.Content?.Files.Remove(file);
        file.Content = content;
        content.Files.Add(file);
        for (var folder = file.Parent; folder is not null; folder = folder.Parent)
            folder.Content = forFolders;
    }

    private static ContentPlan NewContent(List<ContentPlan> contents, ContentDetails details)
    {
        var content = new ContentPlan(contents.Count, details);
        contents.Add(content);
        return content;
    }

    private static FstEntry Scan(string directory)
    {
        var root = new FstEntry("", null, true, 0, null);
        ScanInto(new DirectoryInfo(directory), root);
        return root;
    }

    private static void ScanInto(DirectoryInfo directory, FstEntry parent)
    {
        foreach (var file in directory.GetFiles().OrderBy(f => f.Name, StringComparer.Ordinal))
            parent.Children.Add(new FstEntry(file.Name, file.FullName, false, file.Length, parent));
        foreach (var child in directory.GetDirectories().OrderBy(d => d.Name, StringComparer.Ordinal))
        {
            var entry = new FstEntry(child.Name, child.FullName, true, 0, parent);
            parent.Children.Add(entry);
            ScanInto(child, entry);
        }
    }

    private static void WriteContentHeader(byte[] fst, int offset, ContentPlan content, ContentRecord? record, ref long accumulator)
    {
        BigEndian.Write(fst, offset + 0x00, (uint)accumulator);
        if (content.IsFst)
        {
            accumulator += 2;
            return;
        }
        if (record is null)
            throw new ArgumentException($"Content {content.Index} has not been packed.", nameof(record));

        var units = record.Size / NusFormat.ContentPadding;
        var written = content.Details.Hashed ? Math.Max(0, units - (units / 64 + 1) * 2) : units;
        BigEndian.Write(fst, offset + 0x04, (uint)written);
        BigEndian.Write(fst, offset + 0x08, content.Details.ParentTitleId);
        BigEndian.Write(fst, offset + 0x10, (uint)content.Details.GroupId);
        fst[offset + 0x14] = (byte)(content.Details.Hashed ? 2 : 1);
        accumulator += units;
    }
}
