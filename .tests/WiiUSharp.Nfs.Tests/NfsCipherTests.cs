using System.Security.Cryptography;

namespace WiiUSharp.Nfs.Tests;

[TestClass]
public class NfsCipherTests
{
    private static readonly NfsKey Key = new(Enumerable.Range(1, 16).Select(i => (byte)i).ToArray());

    [TestMethod]
    public void CounterCarriesAcrossBytes()
    {
        Assert.AreEqual("0000001F00", Hex(NfsCipher.IvFor(3)).Substring(22));
        Assert.AreEqual("00002000", Hex(NfsCipher.IvFor(3 + 0x100)).Substring(24));
        Assert.AreEqual("00010000", Hex(NfsCipher.IvFor(3 + 0xE100)).Substring(24));
    }

    [TestMethod]
    public void FirstThreeSectorsUseZeroIv()
    {
        for (var i = 0; i < 3; i++)
            CollectionAssert.AreEqual(new byte[16], NfsCipher.IvFor(i));
    }

    [TestMethod]
    public void FourthSectorStartsTheCounterAt1F00()
    {
        var iv = NfsCipher.IvFor(3);

        CollectionAssert.AreEqual(new byte[12], iv.Take(12).ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x1F, 0x00 }, iv.Skip(12).ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x1F, 0x01 }, NfsCipher.IvFor(4).Skip(12).ToArray());
    }

    [TestMethod]
    public void MatchesPlainAesCbc()
    {
        var plain = Pattern(NfsFormat.SectorSize, 0x5A);
        using var cipher = new NfsCipher(Key);

        var actual = (byte[])plain.Clone();
        cipher.Encrypt(7, actual);

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        aes.Key = Key.ToArray();
        aes.IV = NfsCipher.IvFor(7);
        var expected = aes.CreateEncryptor().TransformFinalBlock(plain, 0, plain.Length);

        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void RejectsBadLengths()
    {
        using var cipher = new NfsCipher(Key);

        Assert.ThrowsExactly<ArgumentException>(() => cipher.Encrypt(0, new byte[0]));
        Assert.ThrowsExactly<ArgumentException>(() => cipher.Encrypt(0, new byte[15]));
        Assert.ThrowsExactly<ArgumentException>(() => cipher.Encrypt(0, new byte[NfsFormat.SectorSize + 16]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => NfsCipher.IvFor(-1));
    }

    [TestMethod]
    public void RoundTripsWholeAndPartialSectors()
    {
        using var cipher = new NfsCipher(Key);
        foreach (var length in new[] { 16, 0x400, NfsFormat.SectorSize })
        {
            var plain = Pattern(length, 0x33);
            var data = (byte[])plain.Clone();

            cipher.Encrypt(5, data);
            CollectionAssert.AreNotEqual(plain, data);
            cipher.Decrypt(5, data);
            CollectionAssert.AreEqual(plain, data);
        }
    }

    [TestMethod]
    public void SectorIndexChangesCiphertextOnlyOnceTheCounterStarts()
    {
        using var cipher = new NfsCipher(Key);
        var plain = Pattern(NfsFormat.SectorSize, 0x11);

        var sector0 = (byte[])plain.Clone();
        var sector2 = (byte[])plain.Clone();
        var sector3 = (byte[])plain.Clone();
        var sector4 = (byte[])plain.Clone();
        cipher.Encrypt(0, sector0);
        cipher.Encrypt(2, sector2);
        cipher.Encrypt(3, sector3);
        cipher.Encrypt(4, sector4);

        CollectionAssert.AreEqual(sector0, sector2);
        CollectionAssert.AreNotEqual(sector2, sector3);
        CollectionAssert.AreNotEqual(sector3, sector4);
    }

    private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "");

    private static byte[] Pattern(int length, byte seed)
    {
        var bytes = new byte[length];
        for (var i = 0; i < length; i++)
            bytes[i] = (byte)(seed + i * 31);
        return bytes;
    }
}
