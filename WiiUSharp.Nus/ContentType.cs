namespace WiiUSharp.Nus;

/// <summary>
/// Type bits of a content record.
/// </summary>
[Flags]
public enum ContentType : ushort
{
    Encrypted = 0x0001,
    Hashed = 0x0002,
    Content = 0x2000,
}
