namespace WiiUSharp.Nus.Tests;

[TestClass]
public class CertificateChainTests
{
    [TestMethod]
    public void Stub_Always_WritesThreeNamedCertificatesAndNothingElse()
    {
        var chain = CertificateChain.Stub();

        Assert.AreEqual(NusFormat.CertificateChainSize, chain.Length);
        Assert.AreEqual(0x00010003u, Reference.ReadUInt32(chain, 0x000));
        Assert.AreEqual(0x00010004u, Reference.ReadUInt32(chain, 0x400));
        Assert.AreEqual(0x00010004u, Reference.ReadUInt32(chain, 0x700));
        CollectionAssert.AreEqual(Reference.Ascii("Root"), Reference.Slice(chain, 0x240, 4));
        Assert.AreEqual(1u, Reference.ReadUInt32(chain, 0x280));
        CollectionAssert.AreEqual(Reference.Ascii("CA00000003"), Reference.Slice(chain, 0x284, 10));
        CollectionAssert.AreEqual(Reference.Ascii("Root-CA00000003"), Reference.Slice(chain, 0x540, 15));
        Assert.AreEqual(1u, Reference.ReadUInt32(chain, 0x580));
        CollectionAssert.AreEqual(Reference.Ascii("CP0000000b"), Reference.Slice(chain, 0x584, 10));
        CollectionAssert.AreEqual(Reference.Ascii("Root-CA00000003"), Reference.Slice(chain, 0x840, 15));
        Assert.AreEqual(1u, Reference.ReadUInt32(chain, 0x880));
        CollectionAssert.AreEqual(Reference.Ascii("XS0000000c"), Reference.Slice(chain, 0x884, 10));

        var nonZero = chain.Select((b, i) => (b, i)).Where(p => p.b != 0).Select(p => p.i).ToArray();
        var expected = new[] { 0x001, 0x003, 0x401, 0x403, 0x701, 0x703, 0x283, 0x583, 0x883 }
            .Concat(Enumerable.Range(0x240, 4)).Concat(Enumerable.Range(0x284, 10))
            .Concat(Enumerable.Range(0x540, 15)).Concat(Enumerable.Range(0x584, 10))
            .Concat(Enumerable.Range(0x840, 15)).Concat(Enumerable.Range(0x884, 10))
            .Where(i => chain[i] != 0).OrderBy(i => i).ToArray();
        CollectionAssert.AreEqual(expected, nonZero);
    }
}
