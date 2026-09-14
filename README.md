# WiiUSharp

.NET libraries for Wii U file formats and title concepts.

Not affiliated with or endorsed by Nintendo. Wii U is a trademark of Nintendo.

The NFS container format was worked out from [nfs2iso2nfs](https://github.com/FIX94/nfs2iso2nfs) by sabykos, piratesephiroth and FIX94. `WiiUSharp.Nfs` is a new implementation of that format, not a port of their code.

## Packages

| Package | Targets | Purpose |
|---|---|---|
| `WiiUSharp` | net48, net6.0, net8.0, net10.0 | Title identity and presentation: `TitleId`, `GroupId`, `ProductCode`, `Region`, `Language`, per-language names, and the `ImageSlot` / `BootSound` formats a title ships with. |
| `WiiUSharp.Nfs` | net48, net6.0, net8.0, net10.0 | The vWii disc container (`content/hif_*.nfs`): header, sparse part table, per-sector AES, 250 MB split. |
| `WiiUSharp.Imaging` | net48, net6.0, net8.0, net10.0 | Turns PNG, JPEG, BMP, WebP or TGA into the exact TGA an `ImageSlot` needs. SkiaSharp + TargaSharp; no System.Drawing. |
| `WiiUSharp.Audio` | net48, net6.0, net8.0, net10.0 | Turns WAV, MP3 or AIFF into `bootSound.btsnd`: 48 kHz stereo 16-bit, six seconds. NAudio.Core + NLayer; no Windows codecs. |

```
dotnet add package WiiUSharp
dotnet add package WiiUSharp.Nfs
dotnet add package WiiUSharp.Imaging
dotnet add package WiiUSharp.Audio
```

## Usage

### Title metadata

```csharp
using WiiUSharp;

var game = new Game(
    new TitleId(TitleType.Demo, 0x12345678),
    new GroupId(0x5678),
    new ProductCode(ProductCode.EShop, "FAAE"))
{
    Region = Region.All,
    Names = LocalizedName.ForAllLanguages(new LocalizedName("Super Metroid")),
};

string id = game.TitleId.ToString();            // "0005000212345678"
ImageSlot icon = ImageSlot.Icon;                // iconTex.tga, 128x128, 32 bpp
```

### Title images

```csharp
using WiiUSharp.Imaging;

TitleImage.Convert("icon.png", ImageSlot.Icon, @"title\meta\iconTex.tga");   // resized to 128x128, 32 bpp, uncompressed, no footer

var problems = TitleImage.Problems(new TgaFile("bootTvTex.tga"), ImageSlot.BootTv);   // empty when it fits
```

### Boot sound

```csharp
using WiiUSharp.Audio;

BootSoundConverter.Convert("boot.mp3", @"title\meta\bootSound.btsnd");   // resampled, stereo, first six seconds

BootSound sound = BootSound.Load(@"title\meta\bootSound.btsnd");          // header + samples of an existing one
```

### NFS container

```csharp
using WiiUSharp.Nfs;

var key = NfsKey.FromFile(@"title\code\htk.bin");

// read: a seekable stream of the decrypted payload, no temp file
using Stream payload = NfsReader.Open(@"title\content", key).OpenPayload();

// write: pack, encrypt and split in one pass
new NfsWriter(key).Write(payload, new DiscDataSpan(0xF800000, gameLength), @"out\content");
```

The payload is a Wii disc image with its game partitions decrypted. Converting to or from a normal ISO needs Wii partition crypto, which `WiiUSharp.Nfs` does not implement — supply an `IPartitionCipher` and use `NfsConverter`. See [.docs/WiiUSharp.Nfs.md](.docs/WiiUSharp.Nfs.md).

## Building

```
dotnet build WiiUSharp.sln
dotnet test WiiUSharp.sln
```

Requires the .NET 10 SDK (`global.json`). `dotnet pack -c Release` produces the NuGet packages.

## License

MIT — see [LICENSE](LICENSE).
