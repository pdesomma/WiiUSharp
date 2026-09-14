using System.Text;

namespace WiiUSharp.Nus;

/// <summary>
/// Builds the placeholder title.cert NUSPacker ships: names only, no key material.
/// </summary>
public static class CertificateChain
{
    /// <summary>
    /// The 0xA00-byte stub.
    /// </summary>
    public static byte[] Stub()
    {
        var chain = new byte[NusFormat.CertificateChainSize];
        Certificate(chain, 0x000, 0x00010003u, "Root", "CA00000003");
        Certificate(chain, 0x400, 0x00010004u, "Root-CA00000003", "CP0000000b");
        Certificate(chain, 0x700, 0x00010004u, "Root-CA00000003", "XS0000000c");
        return chain;
    }

    private static void Certificate(byte[] chain, int offset, uint signatureType, string issuer, string name)
    {
        var nameOffset = signatureType == 0x00010003u ? 0x280 : 0x180;
        BigEndian.Write(chain, offset, signatureType);
        Encoding.ASCII.GetBytes(issuer).CopyTo(chain, offset + nameOffset - 0x40);
        BigEndian.Write(chain, offset + nameOffset, 0x00000001u);
        Encoding.ASCII.GetBytes(name).CopyTo(chain, offset + nameOffset + 4);
    }
}
