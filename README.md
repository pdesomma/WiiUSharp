# WiiUSharp

.NET libraries for Wii U file formats and title concepts.

Not affiliated with or endorsed by Nintendo. Wii U is a trademark of Nintendo.

The NFS container format was worked out from [nfs2iso2nfs](https://github.com/FIX94/nfs2iso2nfs) by sabykos, piratesephiroth and FIX94, the NUS package layout from [NUSPacker](https://github.com/ihaveamac/NUSPacker) by Maschell (timogus) and [CDecrypt](https://github.com/crediar/cdecrypt) by crediar, and RPX compression from [wiiurpxtool](https://github.com/0CBH0/wiiurpxtool) by CBH. `WiiUSharp.Nfs`, `WiiUSharp.Nus` and `WiiUSharp.Rpx` are new implementations of those formats, not ports of their code.

No keys ship with these packages. The common key, title keys and `htk.bin` are yours to supply.

## Packages

| Package | Targets | Purpose |
|---|---|---|
| `WiiUSharp` | net48, net6.0, net8.0, net10.0 | Title identity and presentation (`TitleId`, `GroupId`, `ProductCode`, `Region`, `Language`, per-language names, `ImageSlot` / `BootSound` formats, meta and app XML); `WiiUSharp.Nus`: pack a `code`/`content`/`meta` folder into an installable title (FST, hashed and plain contents, fake-signed TMD and ticket), download a title from the update server, unpack it back; `WiiUSharp.Nfs`: the vWii disc container (`content/hif_*.nfs`); `WiiUSharp.Rpx`: RPX/RPL executables with sections plain, replaceable and re-compressed; `WiiUSharp.Wud`: .wud/.wux disc dumps opened with their game.key, each game title written out as an installable package. No dependencies. |
| `WiiUSharp.Imaging` | net48, net6.0, net8.0, net10.0 | Turns PNG, JPEG, BMP, WebP or TGA into the exact TGA an `ImageSlot` needs. SkiaSharp + TargaSharp; no System.Drawing. |
| `WiiUSharp.Audio` | net48, net6.0, net8.0, net10.0 | Turns WAV, MP3 or AIFF into `bootSound.btsnd`: 48 kHz stereo 16-bit, six seconds. NAudio.Core + NLayer; no Windows codecs. |

`WiiUSharp.Nfs`, `WiiUSharp.Nus` and `WiiUSharp.Rpx` shipped as separate packages up to 0.7.2; from 0.8.0 they live inside `WiiUSharp` under the same namespaces.

```
dotnet add package WiiUSharp
dotnet add package WiiUSharp.Nfs
dotnet add package WiiUSharp.Imaging
dotnet add package WiiUSharp.Audio
dotnet add package WiiUSharp.Nus
dotnet add package WiiUSharp.Rpx
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

using WiiUSharp.Wud;

using Stream image = WudImage.Open("game.wux");               // .wud, .wux or game_part1.wud...
WudDisc disc = WudDisc.Read(image, DiscKey.Beside("game.wux")!.Value);
foreach (WudTitle title in disc.Titles)                      // the game, plus any update or DLC on the disc
    title.WritePackage(image, commonKey, $"install/{title.TitleId}");   // title.tmd, title.tik, title.cert, *.app, *.h3
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

### Downloading and unpacking a title

```csharp
using WiiUSharp.Nus;

var commonKey = CommonKey.Parse("<wii u common key>");
var titleKey = EncryptedTitleKey.Parse("<title key as the ticket stores it>");   // what title key lists carry

var downloader = new NusDownloader(new HttpClient());
await downloader.DownloadAsync(TitleId.Parse("00050000101BAF00"), @"package", titleKey, commonKey);
// package\title.tmd, title.tik (built from the key; retail games have none on the server), 00000000.app ... and .h3 files
// With the common key given, content 0 must decrypt to an FST before anything large is fetched.

new NusUnpacker(commonKey).Unpack(@"package", @"title");   // title\code, content, meta — every hash verified
```

Nothing is needed to download but the title ID; the keys only decrypt. See [.docs/WiiUSharp.Nus.md](.docs/WiiUSharp.Nus.md).

### Executables

```csharp
using WiiUSharp.Rpx;

RpxFile rpx = RpxFile.Load(@"title\code\WUP-JAAE.rpx");   // compressed sections are inflated on load
Section rodata = rpx.FindSection(".rodata")!;
rodata.Data = Patched(rodata.Data);                        // any byte change, any length
rpx.Save(@"title\code\WUP-JAAE.rpx", compress: true);     // CRC table rebuilt, layout as wiiurpxtool writes it
```

See [.docs/WiiUSharp.Rpx.md](.docs/WiiUSharp.Rpx.md).

## Building

```
dotnet build WiiUSharp.sln
dotnet test WiiUSharp.sln
```

Requires the .NET 10 SDK (`global.json`). `dotnet pack -c Release` produces the NuGet packages.

## License

MIT — see [LICENSE](LICENSE).
