# WiiUSharp.Nfs

Reads and writes the Wii U's vWii disc container (`content/hif_000000.nfs …`), replacing the NFS half of `nfs2iso2nfs`.

`nfs2iso2nfs` is three concerns glued together. Only the first is this library:

| Concern | Lives in |
|---|---|
| NFS container: EGGS header, sparse part table, AES layer, 250 MB split | **WiiUSharp.Nfs** |
| Wii partition crypto: title key from common key, hash-table IVs, pad ISO to layer size | WiiSharp, via `IPartitionCipher` |
| `fw.img` patches: `-homebrew -passthrough -wiimote -horizontal -instantcc -nocc -lrpatch` + fakesign | the injector (`PD.WiiU.VirtualConsole`) |

## Format (from FIX94/nfs2iso2nfs)

| Thing | Value |
|---|---|
| Sector | `0x8000` |
| Header | `0x200` bytes, first file only, `0xFF` filled |
| Magic | `"EGGS"` at 0x00, `"SGGE"` at 0x1FC; bytes 0x04–0x07 = `00 01 10 11`; 0x08–0x0F zero |
| Part table | count BE u32 at 0x10; `{startSector, sectorCount}` BE u32 pairs from 0x14 |
| Files | split at `0xFA00000` bytes (header included), named `hif_{n:D6}.nfs`; files 1+ have no header |
| Cipher | AES-128-CBC, no padding, key = `code/htk.bin` (16 bytes), applied per packed sector |
| IV | packed sectors 0–2: zero. Sector `n ≥ 3`: bytes 12–15 = BE `0x1F00 + (n − 3)` |
| Payload | Wii disc image with partition **data decrypted** (hash tables intact); the update partition and every gap are dropped by the part table |

The writer emits the tool's fixed three-part layout: `{0, 1}` disc header, `{8, 2}` partition tables, `{gameStart, gameLength}` from the first game partition's offset to the end of the last one. The IV switch at sector 3 is exactly where game data begins in that layout.

## Pipeline

```
ISO → NFS   IPartitionCipher.Decrypt(iso → payload, returns DiscDataSpan)
            NfsWriter.Write(payload, span, key, contentDir)       pack · encrypt · split, one pass

NFS → ISO   NfsReader.Open(contentDir, key).OpenPayload()         seekable, decrypts and expands gaps on demand
            IPartitionCipher.Encrypt(payload → iso)
            pad to 0x118240000 (single layer) or 0x1FB4E0000 (dual)
```

## Public API

```csharp
namespace WiiUSharp.Nfs;

static class NfsFormat                       // constants above
readonly struct NfsKey                       // 16 bytes; FromFile("htk.bin")
readonly record struct NfsPart(uint StartSector, uint SectorCount)
readonly record struct DiscDataSpan(long Offset, long Length)
sealed class NfsHeader                       // Parts; Parse / TryParse / ToBytes; ForDisc(DiscDataSpan)
static class NfsCipher                       // IvFor(sectorIndex); Encrypt/Decrypt(key, sectorIndex, byte[])
sealed class NfsReader                       // Open(dir, key) → Header, Files, OpenPayload()
sealed class NfsWriter                       // Write(Stream payload, DiscDataSpan, NfsKey, dir, IProgress<long>?, ct)
interface IPartitionCipher                   // DiscDataSpan Decrypt(iso, payload, ct); void Encrypt(payload, iso, ct)
sealed class NfsConverter                    // ToIso / FromIso composing the above
```

## Tests

Synthetic only — no Nintendo fixtures. Header byte-for-byte against the tool's layout, IV known-answers around the sector-3 switch and the byte-15 carry, split boundary at exactly `0xFA00000`, write→read round-trip including gaps, cipher round-trip.

## Open questions (check against a real base)

1. Header bytes 0x04–0x0F — version/flags? The tool hardcodes them.
2. Real Nintendo NFS files may carry more than three parts; the reader handles N, the IV rule is applied by packed sector index as the tool does.
3. Confirm retail vWii titles really store partition data decrypted (the tool assumes it).

## Source

FIX94/nfs2iso2nfs `Program.cs`: `packNFS`, `unpackNFS`, `EnDecryptNFS`, `manipulateISO`, `splitNFSFile`, `combineNFSFiles`.
