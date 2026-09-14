# WiiUSharp.Rpx

Reads and writes RPX/RPL executables, replacing `wiiurpxtool -d` / `-c`.

An RPX is a big-endian ELF32 (`e_type` 0xFE01, PowerPC) whose sections may be stored zlib-compressed. `RpxFile` loads every section plain, lets a caller replace section bytes, and saves either plain or compressed with the CRC table rebuilt.

## Format facts

| Thing | Value |
|---|---|
| ELF header | 0x34 bytes; section headers 0x28 bytes each at `e_shoff`, normally 0x40 |
| Compressed section | `sh_flags & 0x08000000`; data = BE u32 plain length, then a zlib stream; `sh_size` is the stored (compressed + 4) length |
| RPL section types | `0x80000001` exports, `0x80000002` imports, `0x80000003` CRCs, `0x80000004` file info |
| CRC table | one BE u32 per section, in section order: CRC-32 (zlib polynomial) of the **plain** data; 0 for the CRC section itself and for sections with no file data |
| Layout on write | header, zero to the end of the section table, then every section with a non-zero offset in ascending original-offset order, each followed by zero padding to 0x40 |
| What gets compressed | everything except the CRC and file-info sections; a section under 16 KiB stays plain unless compressing saves at least 5 bytes |
| NOBITS | no data read or written; `sh_size` is kept |

## Public API

```
RpxFile.Load(path | stream) / Parse(bytes)   → Header, Sections, NameOf(section), FindSection(name)
Section.Data { get; set; }                   plain bytes; Type, Flags (zlib bit removed), Address, StoredCompressed, StoredSize
RpxFile.Save(path | stream, compress)
Crc32.Compute / Update
```

## Credits

Format facts from wiiurpxtool by CBH (0CBH0), GPL-3; this library is a fresh implementation. RPL section types per WiiUBrew.
