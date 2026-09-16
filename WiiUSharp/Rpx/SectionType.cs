namespace WiiUSharp.Rpx;

/// <summary>
/// ELF section types, including the RPL-specific ones.
/// </summary>
public enum SectionType : uint
{
    Null = 0x00000000,
    ProgBits = 0x00000001,
    SymTab = 0x00000002,
    StrTab = 0x00000003,
    Rela = 0x00000004,
    NoBits = 0x00000008,
    RplExports = 0x80000001,
    RplImports = 0x80000002,
    RplCrcs = 0x80000003,
    RplFileInfo = 0x80000004,
}
