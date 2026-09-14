namespace WiiUSharp.Nus.Tests;

[TestClass]
public class TitleLayoutTests
{
    private static readonly TitleInfo Info = new(Reference.TitleId, 0x1ABC);

    private string _root = null!;

    [TestInitialize]
    public void Initialize() => _root = FakeTitle.Create(Reference.TempRoot());

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, recursive: true);

    [TestMethod]
    public void Build_CommonRules_AssignsContentsInRuleOrderWithLaterRulesWinning()
    {
        var layout = TitleLayout.Build(_root, ContentRules.Common(Info));

        var contents = layout.Contents.Select(c => string.Join(",", c.Files.Select(f => f.TitlePath))).ToArray();
        CollectionAssert.AreEqual(new[]
        {
            "",
            "/code/app.xml",
            "/code/cos.xml",
            "/meta/meta.xml",
            "/meta/iconTex.tga",
            "/meta/bootLogoTex.tga",
            "/code/game.rpx",
            "/code/fw.img",
            "/content/a.bin,/content/sub/b.bin,/content/sub/hif_000000.nfs",
        }, contents);
        Assert.IsTrue(layout.Contents[0].IsFst);
        Assert.IsTrue(layout.Contents[3].Details.Hashed);
        Assert.IsFalse(layout.Contents[7].Details.Hashed);
        Assert.AreEqual(0x1ABC, layout.Contents[8].Details.GroupId);
        Assert.AreEqual(0x000500001ABCDE00UL, layout.Contents[8].Details.ParentTitleId);
        CollectionAssert.AreEqual(new[] { 0L, 0x80L, 0xFCC0L }, layout.Contents[8].Files.Select(f => f.Offset).ToArray());
        Assert.AreEqual(0xFD00L, layout.Contents[8].PlainLength);
    }

    [TestMethod]
    public void Build_CommonRules_NumbersEntriesDepthFirstFilesBeforeFolders()
    {
        var layout = TitleLayout.Build(_root, ContentRules.Common(Info));

        var entries = layout.Entries;
        CollectionAssert.AreEqual(new[]
        {
            "", "/code", "/code/app.xml", "/code/cos.xml", "/code/fw.img", "/code/game.rpx",
            "/content", "/content/a.bin", "/content/sub", "/content/sub/b.bin", "/content/sub/hif_000000.nfs",
            "/meta", "/meta/bootLogoTex.tga", "/meta/iconTex.tga", "/meta/meta.xml",
        }, entries.Select(e => e.TitlePath).ToArray());
        CollectionAssert.AreEqual(Enumerable.Range(0, 15).ToArray(), entries.Select(e => e.Index).ToArray());
        Assert.AreEqual(7, entries[1].Content!.Index, "code folder takes the last matching rule");
        Assert.AreEqual(8, entries[8].Content!.Index);
        Assert.AreEqual(5, entries[11].Content!.Index);
        Assert.AreEqual(0x02, entries[10].Type);
        Assert.AreEqual(0x01, entries[8].Type);
        Assert.AreEqual(0x00, entries[9].Type);
    }

    [TestMethod]
    public void Build_EmptyFolder_ThrowsInvalidDataException()
    {
        Directory.CreateDirectory(Path.Combine(_root, "content", "empty"));

        var error = Assert.ThrowsExactly<InvalidDataException>(() => TitleLayout.Build(_root, ContentRules.Common(Info)));

        StringAssert.Contains(error.Message, "/content/empty");
    }

    [TestMethod]
    public void Build_FileMatchingNoRule_ThrowsInvalidDataException()
    {
        File.WriteAllBytes(Path.Combine(_root, "stray.bin"), new byte[4]);

        var error = Assert.ThrowsExactly<InvalidDataException>(() => TitleLayout.Build(_root, ContentRules.Common(Info)));

        StringAssert.Contains(error.Message, "/stray.bin");
    }

    [TestMethod]
    public void Build_OneContentRulePastMaxLength_SplitsIntoMoreContents()
    {
        var rules = new[] { new ContentRule("/.*", new ContentDetails(false, 0, 0, 0)) };

        var layout = TitleLayout.Build(_root, rules, maxContentLength: 100000);

        CollectionAssert.AreEqual(new[] { 0, 3, 2, 5 }, layout.Contents.Select(c => c.Files.Count).ToArray());
        Assert.AreEqual("/code/game.rpx", layout.Contents[2].Files[0].TitlePath);
        Assert.AreEqual(1, layout.Entries[1].Content!.Index, "folders take the first content of the rule");
    }

    [TestMethod]
    public void BuildFst_CommonRules_WritesHeaderContentTableEntriesAndStrings()
    {
        var layout = TitleLayout.Build(_root, ContentRules.Common(Info));
        var records = new ContentRecord?[]
        {
            null,
            Record(1, false, 0x8000), Record(2, false, 0x8000), Record(3, true, 0x10000), Record(4, true, 0x10000),
            Record(5, true, 0x10000), Record(6, false, 0x18000), Record(7, false, 0x10000), Record(8, true, 0x20000),
        };

        var fst = layout.BuildFst(records);

        Assert.AreEqual(NusFormat.ContentPadding, fst.Length);
        CollectionAssert.AreEqual(new byte[] { 0x46, 0x53, 0x54, 0x00 }, Reference.Slice(fst, 0, 4));
        Assert.AreEqual(0x20u, Reference.ReadUInt32(fst, 4));
        Assert.AreEqual(9u, Reference.ReadUInt32(fst, 8));

        AssertContentHeader(fst, 0, 0, 0, 0, 0, 0);
        AssertContentHeader(fst, 1, 2, 1, 0, 0, 1);
        AssertContentHeader(fst, 2, 3, 1, 0, 0, 1);
        AssertContentHeader(fst, 3, 4, 0, 0, 0x400, 2);
        AssertContentHeader(fst, 6, 10, 3, 0, 0, 1);
        AssertContentHeader(fst, 7, 13, 2, 0, 0, 1);
        AssertContentHeader(fst, 8, 15, 2, 0x000500001ABCDE00UL, 0x1ABC, 2);

        var entries = 0x20 + 9 * 0x20;
        CollectionAssert.AreEqual(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 15, 0, 0, 0, 0 }, Reference.Slice(fst, entries, 16), "root");
        AssertFolder(fst, entries + 1 * 16, "code", parent: 0, next: 6, flags: 0, content: 7);
        AssertFile(fst, entries + 2 * 16, "app.xml", offset: 0, size: FakeTitle.Files["code/app.xml"].Length, flags: 0, content: 1);
        AssertFolder(fst, entries + 6 * 16, "content", parent: 0, next: 11, flags: 0x400, content: 8);
        AssertFolder(fst, entries + 8 * 16, "sub", parent: 6, next: 11, flags: 0x400, content: 8);
        AssertFile(fst, entries + 9 * 16, "b.bin", offset: 0x80, size: NusFormat.HashBlockDataSize + 50, flags: 0x400, content: 8);
        Assert.AreEqual(0x02, fst[entries + 10 * 16]);
        AssertFolder(fst, entries + 11 * 16, "meta", parent: 0, next: 15, flags: 0x40, content: 5);
        AssertFile(fst, entries + 13 * 16, "iconTex.tga", offset: 0, size: 640, flags: 0x40, content: 4);

        var strings = entries + 15 * 16;
        Assert.AreEqual(0, fst[strings]);
        CollectionAssert.AreEqual(Reference.Ascii("code\0app.xml\0"), Reference.Slice(fst, strings + 1, 13));
    }

    private static void AssertContentHeader(byte[] fst, int index, uint offset, uint size, ulong parent, uint group, byte kind)
    {
        var at = 0x20 + index * 0x20;
        Assert.AreEqual(offset, Reference.ReadUInt32(fst, at), $"content {index} offset");
        Assert.AreEqual(size, Reference.ReadUInt32(fst, at + 4), $"content {index} size");
        Assert.AreEqual(parent, Reference.ReadUInt64(fst, at + 8), $"content {index} parent");
        Assert.AreEqual(group, Reference.ReadUInt32(fst, at + 0x10), $"content {index} group");
        Assert.AreEqual(kind, fst[at + 0x14], $"content {index} kind");
        CollectionAssert.AreEqual(new byte[11], Reference.Slice(fst, at + 0x15, 11));
    }

    private static void AssertFile(byte[] fst, int at, string name, long offset, long size, ushort flags, int content)
    {
        Assert.AreEqual(0x00, fst[at], name);
        Assert.AreEqual(name, NameAt(fst, at));
        Assert.AreEqual((uint)(offset >> 5), Reference.ReadUInt32(fst, at + 4), name + " offset");
        Assert.AreEqual((uint)size, Reference.ReadUInt32(fst, at + 8), name + " size");
        Assert.AreEqual(flags, Reference.ReadUInt16(fst, at + 12), name + " flags");
        Assert.AreEqual(content, Reference.ReadUInt16(fst, at + 14), name + " content");
    }

    private static void AssertFolder(byte[] fst, int at, string name, int parent, int next, ushort flags, int content)
    {
        Assert.AreEqual(0x01, fst[at], name);
        Assert.AreEqual(name, NameAt(fst, at));
        Assert.AreEqual((uint)parent, Reference.ReadUInt32(fst, at + 4), name + " parent");
        Assert.AreEqual((uint)next, Reference.ReadUInt32(fst, at + 8), name + " next");
        Assert.AreEqual(flags, Reference.ReadUInt16(fst, at + 12), name + " flags");
        Assert.AreEqual(content, Reference.ReadUInt16(fst, at + 14), name + " content");
    }

    private static string NameAt(byte[] fst, int entryAt)
    {
        var nameOffset = fst[entryAt + 1] << 16 | fst[entryAt + 2] << 8 | fst[entryAt + 3];
        var strings = 0x20 + 9 * 0x20 + 15 * 16 + nameOffset;
        var end = Array.IndexOf(fst, (byte)0, strings);
        return System.Text.Encoding.UTF8.GetString(fst, strings, end - strings);
    }

    private static ContentRecord Record(int index, bool hashed, long size) =>
        new(index, ContentType.Content | ContentType.Encrypted | (hashed ? ContentType.Hashed : 0), size, new byte[20]);
}
