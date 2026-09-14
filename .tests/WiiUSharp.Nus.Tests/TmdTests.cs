namespace WiiUSharp.Nus.Tests;

[TestClass]
public class TmdTests
{
    private static readonly TitleInfo Info = new(Reference.TitleId, 0x1ABC, titleVersion: 0x0011, osVersion: 0x000500101000400A, appType: 0x80000000);

    [TestMethod]
    public void Build_EmptyContents_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Tmd.Build(Info, Array.Empty<ContentRecord>()));
    }

    [TestMethod]
    public void Build_NullArguments_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Tmd.Build(null!, new[] { Record(0) }));
        Assert.ThrowsExactly<ArgumentNullException>(() => Tmd.Build(Info, null!));
    }

    [TestMethod]
    public void Build_RecordsOutOfOrder_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Tmd.Build(Info, new[] { Record(1), Record(0) }));
    }

    [TestMethod]
    public void Build_TwoContents_WritesHeaderInfosAndRecords()
    {
        var records = new[] { Record(0), Record(1, ContentType.Content | ContentType.Encrypted | ContentType.Hashed, 0x20000) };

        var tmd = Tmd.Build(Info, records);

        Assert.AreEqual(Tmd.ContentRecordsOffset + 2 * NusFormat.ContentRecordSize, tmd.Length);
        Assert.AreEqual(0x00010004u, Reference.ReadUInt32(tmd, 0));
        CollectionAssert.AreEqual(new byte[0x100], Reference.Slice(tmd, 4, 0x100));
        CollectionAssert.AreEqual(Reference.Ascii("Root-CA00000003-CP0000000b"), Reference.Slice(tmd, 0x140, 26));
        Assert.AreEqual(0x01, tmd[0x180]);
        Assert.AreEqual(0x000500101000400AUL, Reference.ReadUInt64(tmd, 0x184));
        Assert.AreEqual(Reference.TitleId.Value, Reference.ReadUInt64(tmd, Tmd.TitleIdOffset));
        Assert.AreEqual(0x00000100u, Reference.ReadUInt32(tmd, 0x194));
        Assert.AreEqual(0x1ABC, Reference.ReadUInt16(tmd, 0x198));
        Assert.AreEqual(0x80000000u, Reference.ReadUInt32(tmd, 0x19A));
        Assert.AreEqual(0u, Reference.ReadUInt32(tmd, 0x1D8));
        Assert.AreEqual(0x0011, Reference.ReadUInt16(tmd, 0x1DC));
        Assert.AreEqual(2, Reference.ReadUInt16(tmd, 0x1DE));
        Assert.AreEqual(0, Reference.ReadUInt16(tmd, 0x1E0));

        var recordBytes = records[0].ToBytes().Concat(records[1].ToBytes()).ToArray();
        CollectionAssert.AreEqual(recordBytes, Reference.Slice(tmd, Tmd.ContentRecordsOffset, recordBytes.Length));

        var infos = Reference.Slice(tmd, NusFormat.TmdHeaderSize, NusFormat.ContentInfoCount * NusFormat.ContentInfoSize);
        Assert.AreEqual(0, Reference.ReadUInt16(infos, 0));
        Assert.AreEqual(2, Reference.ReadUInt16(infos, 2));
        CollectionAssert.AreEqual(Reference.Sha256(recordBytes), Reference.Slice(infos, 4, 32));
        CollectionAssert.AreEqual(new byte[infos.Length - NusFormat.ContentInfoSize], Reference.Slice(infos, NusFormat.ContentInfoSize, infos.Length - NusFormat.ContentInfoSize));
        CollectionAssert.AreEqual(Reference.Sha256(infos), Reference.Slice(tmd, 0x1E4, 32));
    }

    [TestMethod]
    public void ContentRecord_ToBytes_LaysOutIdIndexTypeSizeHash()
    {
        var hash = Reference.Pattern(20, 3);

        var bytes = new ContentRecord(0x102, ContentType.Content | ContentType.Encrypted | ContentType.Hashed, 0x1_2345_6780, hash).ToBytes();

        Assert.AreEqual(NusFormat.ContentRecordSize, bytes.Length);
        Assert.AreEqual(0x102u, Reference.ReadUInt32(bytes, 0));
        Assert.AreEqual(0x102, Reference.ReadUInt16(bytes, 4));
        Assert.AreEqual(0x2003, Reference.ReadUInt16(bytes, 6));
        Assert.AreEqual(0x1_2345_6780UL, Reference.ReadUInt64(bytes, 8));
        CollectionAssert.AreEqual(hash, Reference.Slice(bytes, 0x10, 20));
        CollectionAssert.AreEqual(new byte[12], Reference.Slice(bytes, 0x24, 12));
    }

    [TestMethod]
    public void ContentRecord_BadHashLength_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new ContentRecord(0, ContentType.Content, 0, new byte[19]));
    }

    [TestMethod]
    public void Parse_BuiltTmd_RoundTripsTitleAndRecords()
    {
        var records = new[] { Record(0), Record(1, ContentType.Content | ContentType.Encrypted | ContentType.Hashed, 0x20000), new ContentRecord(2, ContentType.Content | ContentType.Encrypted, 0x8000, Reference.Pattern(20, 2), 0xABCD1234) };

        var parsed = Tmd.Parse(Tmd.Build(Info, records));

        Assert.AreEqual(Info.TitleId, parsed.Title.TitleId);
        Assert.AreEqual(Info.GroupId, parsed.Title.GroupId);
        Assert.AreEqual(Info.TitleVersion, parsed.Title.TitleVersion);
        Assert.AreEqual(Info.OsVersion, parsed.Title.OsVersion);
        Assert.AreEqual(Info.AppType, parsed.Title.AppType);
        Assert.AreEqual(3, parsed.Contents.Count);
        for (var i = 0; i < 3; i++)
        {
            Assert.AreEqual(records[i].Index, parsed.Contents[i].Index);
            Assert.AreEqual(records[i].Id, parsed.Contents[i].Id);
            Assert.AreEqual(records[i].Type, parsed.Contents[i].Type);
            Assert.AreEqual(records[i].Size, parsed.Contents[i].Size);
            CollectionAssert.AreEqual(records[i].Hash, parsed.Contents[i].Hash);
        }
        Assert.IsTrue(parsed.Contents[1].IsHashed);
        Assert.AreEqual(0xABCD1234u, Reference.ReadUInt32(records[2].ToBytes(), 0));
    }

    [TestMethod]
    public void Parse_BadBytes_Throws()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => Tmd.Parse(new byte[Tmd.ContentRecordsOffset - 1]));
        Assert.ThrowsExactly<InvalidDataException>(() => Tmd.Parse(new byte[Tmd.ContentRecordsOffset]));
        var tmd = Tmd.Build(Info, new[] { Record(0), Record(1) });
        Assert.ThrowsExactly<InvalidDataException>(() => Tmd.Parse(tmd.Take(tmd.Length - 1).ToArray()));
        Assert.ThrowsExactly<ArgumentNullException>(() => Tmd.Parse(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => ContentRecord.Parse(null!, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ContentRecord.Parse(new byte[10], 0));
        Assert.ThrowsExactly<ArgumentNullException>(() => new TmdInfo(null!, Array.Empty<ContentRecord>()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new TmdInfo(Info, null!));
    }

    private static ContentRecord Record(int index, ContentType type = ContentType.Content | ContentType.Encrypted, long size = 0x8000) =>
        new(index, type, size, Reference.Pattern(20, index));
}
