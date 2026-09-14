# WiiUSharp.Nus

Packs an unpacked title (`code/`, `content/`, `meta/`) into the installable NUS layout, replacing NUSPacker / CNUSPACKER.

Output: `title.tmd`, `title.tik`, `title.cert`, one `{index:X8}.app` per content and a `{index:X8}.h3` for each hashed content. Everything is fake-signed; installing needs signature patches, as it always has.

## Inputs

| Input | Source |
|---|---|
| Title ID, group ID, title version, OS version, app type | `code/app.xml` (`title_id`, `group_id`, `title_version`, `os_version`, `app_type`) via `TitleInfo.FromAppXml` |
| Title key | caller; the 16 bytes every content is encrypted with |
| Common key | caller; only used to encrypt the title key into the ticket. Never stored in the repo |
| Certificate chain | generated stub (below), or caller-supplied bytes |

## Content assignment

Files are grouped into contents by rules, applied in order over paths like `/code/app.xml`. A later rule that matches a file already assigned wins. Each rule either makes **one content** for all its matches or **one content per match**. `ContentRules.Common(groupId, parentTitleId)` is NUSPacker's table:

| # | Pattern | Hashed | Group | Parent title | Entry flags |
|---|---|---|---|---|---|
| 1 | `/code/app.xml` | no | 0 | 0 | 0 |
| 2 | `/code/cos.xml` | no | 0 | 0 | 0 |
| 3 | `/meta/meta.xml` | yes | 0x400 | 0 | 0x40 |
| 4 | `/meta/.*[^.xml)]+` | yes | 0x400 | 0 | 0x40 |
| 5 | `/meta/bootMovie.h264` | yes | 0x400 | 0 | 0x40 |
| 6 | `/meta/bootLogoTex.tga` | yes | 0x400 | 0 | 0x40 |
| 7 | `/meta/Manual.bfma` | yes | 0x400 | 0 | 0x40 |
| 8 | `/meta/.*.jpg` | yes | 0x400 | 0 | 0x40 |
| 9 | `/code/.*(.rpx\|.rpl)` — per match | no | 0 | 0 | 0 |
| 10 | `/code/preload.txt` | yes | 0 | 0 | 0 |
| 11–15 | `/code/fw.img`, `fw.tmd`, `htk.bin`, `rvlt.tik`, `rvlt.tmd` | no | 0 | 0 | 0 |
| 16 | `/content/.*` | yes | *groupId* | *parentTitleId* | 0x400 |

`parentTitleId` = title ID with the type nibble cleared (`& ~0x0000000F00000000`). A one-content rule that grows past `0xBFFFFFFF × 0.975` bytes starts another content. A rule with no match makes no content. A file matched by no rule, or an empty folder, is an error. Content 0 is always the FST.

Content index = position in the list. Content type = `0x2000 | 0x0001` (content, encrypted) `| 0x0002` when hashed.

## FST (content 0)

Big-endian throughout. Padded to `0x8000`.

```
0x00  "FST\0"
0x04  u32 0x20
0x08  u32 content count
0x0C  0x14 zero
0x20  content headers, 0x20 each (see below)
      entries, 0x10 each
      string table, NUL-terminated names, root name is ""
      zero to 0x8000
```

Content header `{u32 offset, u32 size, u64 parentTitleId, u32 groupId, u8 kind, 11 zero}`. `kind` is 0 for the FST, 1 unhashed, 2 hashed. Sizes are in `0x8000` units of the encrypted `.app`; a hashed content reports `size − (size / 64 + 1) × 2`. Offsets accumulate: the FST content writes `{0, 0}` and advances the accumulator by 2, every other content advances by its (unadjusted) size.

Entries are depth-first: root, then for each folder its files first, then its subfolders. Root is `{0x01, 7 zero, u32 entryCount, 4 zero}`. Others:

```
0x00  u8  type: 0x01 folder; |= 0x02 when the name ends in "nfs"
0x01  u24 name offset in the string table
0x04  folder: u32 parent entry index      file: u32 offset in content >> 5
0x08  folder: u32 first index after subtree   file: u32 size
0x0C  u16 flags (from the content rule)
0x0E  u16 content index
```

A folder's content index is that of the last rule that matched something under it. Within a content, files sit in entry order, each aligned to `0x20`; the plaintext content is those files concatenated with that padding.

## Content encryption

AES-128-CBC, no padding, title key.

**Unhashed** (`0x2001`): plaintext zero-padded to `0x8000`, one CBC chain over the whole file, IV = `u16 index` followed by 14 zero bytes. TMD hash = SHA-1 of the padded plaintext. TMD size = padded length.

**Hashed** (`0x2003`): plaintext cut into `0xFC00` blocks (last one zero-padded; an empty content is one zero block). Hash tree, all SHA-1:

```
H0[b]  = SHA1(block b)
H1[j]  = SHA1(H0[16j … 16j+15])   missing entries hash as 20 zero bytes
H2[k]  = SHA1(H1[16k … 16k+15])
H3[l]  = SHA1(H2[16l … 16l+15])
```

Each level has `ceil(previous / 16)` entries. Every output block is `0x10000`: a `0x400` hash header then the `0xFC00` data.

```
header = H0[16·(b/16) …+16] ‖ H1[16·(b/256) …+16] ‖ H2[16·(b/4096) …+16] ‖ zero to 0x400
header[1] ^= (byte)index
encrypt header with IV = u16 index ‖ 14 zero            (fresh IV every block)
encrypt data   with IV = first 16 bytes of H0[b]        (the un-XORed hash)
```

`.h3` = the H3 entries concatenated. TMD hash = SHA-1 of that file. TMD size = blocks × `0x10000`.

NUSPacker pads the last hashed block with whatever the previous block left in its buffer and hashes an extra block when the length is an exact multiple; this library zero-pads and does not. Both are self-consistent, and the console only reads the ranges the FST names.

## title.tmd

```
0x000  u32 0x00010004               signature type (RSA-2048 SHA-256)
0x004  0x100 zero                    signature
0x104  0x3C zero
0x140  "Root-CA00000003-CP0000000b"  zero-padded to 0x40
0x180  u8 1, u8 0, u8 0, u8 0        version, CA CRL, signer CRL, pad
0x184  u64 OS version                app.xml os_version; NUSPacker default 0x000500101000400A
0x18C  u64 title ID
0x194  u32 0x00000100                title type
0x198  u16 group ID
0x19A  u32 app type                  app.xml app_type; NUSPacker default 0x80000000
0x19E  u32 0, u32 0, 50 zero
0x1D8  u32 0                         access rights
0x1DC  u16 title version
0x1DE  u16 content count
0x1E0  u16 0                         boot index
0x1E2  2 zero
0x1E4  SHA-256 of the content-info table
0x204  64 × {u16 index offset, u16 count, 32-byte SHA-256}: [0] = {0, contentCount, SHA-256 of the content records}, rest zero
0xB04  content records, 0x30 each: {u32 id, u16 index, u16 type, u64 size, 20-byte SHA-1, 12 zero}
```

No certificate chain is appended.

## title.tik (0x350)

```
0x000  u32 0x00010004
0x004  0x100 random                  signature
0x104  0x3C zero
0x140  "Root-CA00000003-XS0000000c"  zero-padded to 0x20
0x160  0x5C zero
0x1BC  01 00 00                      format version 1
0x1BF  encrypted title key           AES-128-CBC(common key, IV = title ID ‖ 8 zero) of the title key
0x1CF  00 05, 6 random               unknown byte + ticket ID
0x1D8  4 zero                        console ID
0x1DC  u64 title ID
0x1E4  00 00 00 11 00 00 00 00 00 00 00 00 00 00 00 05
0x1F4  0xB0 zero
0x2A4  00 01 00 14 00 00 00 AC 00 00 00 14 00 01 00 14 00 00 00 00 00 00 00 28 00 00 00 01 00 00 00 84 00 00 00 84 00 03 00 00 00 00 00 00 FF FF FF 01
0x2D4  0x7C zero
```

## title.cert (0xA00)

NUSPacker's stub carries no key material, only names, so it is safe to generate:

```
0x000  u32 0x00010003          0x240 "Root"             0x280 u32 1, "CA00000003"
0x400  u32 0x00010004          0x540 "Root-CA00000003"  0x580 u32 1, "CP0000000b"
0x700  u32 0x00010004          0x840 "Root-CA00000003"  0x880 u32 1, "XS0000000c"
```
Names are zero-padded to 16 bytes; everything else is zero. A caller can pass a real chain instead.

## Public API

```
NusPacker(CommonKey commonKey)
  .Pack(string titleDirectory, string outputDirectory, TitleKey titleKey,
        TitleInfo? info = null,                         // default: read code/app.xml
        IReadOnlyList<ContentRule>? rules = null,       // default: ContentRules.Common
        byte[]? certificateChain = null,                // default: the stub
        IProgress<string>? progress = null, CancellationToken cancellationToken = default)
  → NusPackage { OutputDirectory, TitleInfo, Contents }

TitleInfo(TitleId titleId, ushort groupId, ushort titleVersion, ulong osVersion, uint appType)
  .FromAppXml(AppXml) / .FromAppXml(path)
ContentRule(string pattern, ContentDetails details, bool contentPerMatch = false)
ContentDetails(bool hashed, ushort groupId, ulong parentTitleId, ushort entryFlags)
ContentRules.Common(ushort groupId, ulong parentTitleId)
Ticket.Build(titleId, titleKey, commonKey) → byte[]
Tmd.Build(TitleInfo, IReadOnlyList<ContentRecord>) → byte[]
CertificateChain.Stub() → byte[]
```

## Credits

Format facts from NUSPacker by Maschell (timogus), as maintained by ihaveamac; CNUSPACKER by NicoAICP, ZestyTS and Morilli is its C# port. This library is a fresh implementation of the format.
