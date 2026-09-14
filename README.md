# WiiUSharp

.NET libraries for Wii U file formats and title concepts.

Not affiliated with or endorsed by Nintendo. Wii U is a trademark of Nintendo.

The NFS container format was worked out from [nfs2iso2nfs](https://github.com/FIX94/nfs2iso2nfs) by sabykos, piratesephiroth and FIX94, and the NUS package layout from [NUSPacker](https://github.com/ihaveamac/NUSPacker) by Maschell (timogus). `WiiUSharp.Nfs` and `WiiUSharp.Nus` are new implementations of those formats, not ports of their code.

No keys ship with these packages. The common key, title keys and `htk.bin` are yours to supply.

## Packages

| Package | Targets | Purpose |
|---|---|---|
| `WiiUSharp` | net48, net6.0, net8.0, net10.0 | Title identity and presentation: `TitleId`, `GroupId`, `ProductCode`, `Region`, `Language`, per-language names, and the `ImageSlot` / `BootSound` formats a title ships with. |
| `WiiUSharp.Nfs` | net48, net6.0, net8.0, net10.0 | The vWii disc container (`content/hif_*.nfs`): header, sparse part table, per-sector AES, 250 MB split. |
| `WiiUSharp.Imaging` | net48, net6.0, net8.0, net10.0 | Turns PNG, JPEG, BMP, WebP or TGA into the exact TGA an `ImageSlot` needs. SkiaSharp + TargaSharp; no System.Drawing. |
| `WiiUSharp.Audio` | net48, net6.0, net8.0, net10.0 | Turns WAV, MP3 or AIFF into `bootSound.btsnd`: 48 kHz stereo 16-bit, six seconds. NAudio.Core + NLayer; no Windows codecs. |
| `WiiUSharp.Nus` | net48, net6.0, net8.0, net10.0 | Packs a `code`/`content`/`meta` folder into an installable title: FST, hashed and plain contents, fake-signed TMD and ticket. |

```
dotnet add package WiiUSharp
dotnet add package WiiUSharp.Nfs
dotnet add package WiiUSharp.Imaging
dotnet add package WiiUSharp.Audio
dotnet add package WiiUSharp.Nus
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

### Packing a title

```csharp
using WiiUSharp.Nus;

var packer = new NusPacker(CommonKey.Parse("<wii u common key>"));
NusPackage package = packer.Pack(@"title", @"out", TitleKey.Parse("13371337133713371337133713371337"));
// out\title.tmd, title.tik, title.cert, 00000000.app (FST), 00000001.app ... and a .h3 per hashed content
```

Title values come from `code/app.xml`; pass a `TitleInfo` to override them, your own `ContentRule` list to change which files share a content, or a real `title.cert` to replace the generated stub. Output is fake-signed and installs with the usual signature patches. See [.docs/WiiUSharp.Nus.md](.docs/WiiUSharp.Nus.md).

## Building

```
dotnet build WiiUSharp.sln
dotnet test WiiUSharp.sln
```

Requires the .NET 10 SDK (`global.json`). `dotnet pack -c Release` produces the NuGet packages.

## License

MIT — see [LICENSE](LICENSE).
