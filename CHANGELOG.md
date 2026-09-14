# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [0.6.0] - 2026-09-14

### Added
- `WiiUSharp.Rpx`: `RpxFile` reads RPX/RPL executables with every section plain, lets section bytes be replaced, and saves plain or zlib-compressed with the per-section CRC table rebuilt. Replaces wiiurpxtool.

## [0.5.0] - 2026-09-14

### Added
- `WiiUSharp.Nus`: `NusPacker` turns a code/content/meta folder into an installable title: content rules, FST, H0-H3 hashed contents, plain contents, fake-signed TMD and ticket, stub certificate chain. A fresh implementation of the layout NUSPacker produces; no keys or certificates ship in the package.

## [0.4.0] - 2026-09-14

### Added
- `BootSound` is now the .btsnd file model (header target and loop start, big-endian stereo PCM) with `Parse`/`Load`/`ToBytes`/`Save`; the format constants stay where they were.
- `WiiUSharp.Audio`: `BootSoundConverter` decodes WAV, MP3 or AIFF, converts to 48 kHz stereo, trims to six seconds and writes a `BootSound`. NAudio.Core + NLayer, no Windows codecs.

## [0.3.0] - 2026-09-14

### Added
- `WiiUSharp.Imaging`: `TitleImage` loads PNG/JPEG/BMP/WebP/TGA, resizes to an `ImageSlot` and writes uncompressed footer-less TGA at the slot's depth; `Problems`/`Verify` check an existing TGA. SkiaSharp + TargaSharp core, no System.Drawing.

## [0.2.0] - 2026-09-14

### Added
- `MetaXml` and `AppXml`: load a title's meta/meta.xml and code/app.xml, `Apply(Game)` to rewrite identity, region, GamePad use and names, `MetaXml.Read()` to get a `Game` back, `Get`/`Set` for any other field. Saves without a BOM and leaves untouched fields byte-for-byte.

## [0.1.1] - 2026-09-14

### Changed
- README wording and nfs2iso2nfs attribution. No code changes.

## [0.1.0] - 2026-09-14

### Added
- `WiiUSharp`: `Game`, `TitleId`, `TitleType`, `GroupId`, `ProductCode`, `Region`, `Language`, `LocalizedName`, `ImageSlot`, `BootSound`.
- `WiiUSharp.Nfs`: `NfsHeader`, `NfsCipher`, `NfsReader` / `NfsPayloadStream`, `NfsWriter`, `NfsConverter`, `IPartitionCipher`.
