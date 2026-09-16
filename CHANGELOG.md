# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [0.8.0] - 2026-09-15

### Changed
- `WiiUSharp.Nfs`, `WiiUSharp.Nus` and `WiiUSharp.Rpx` are part of the `WiiUSharp` package now; the namespaces are unchanged, so only package references move. The three old packages are retired at 0.7.2.

## [0.7.2] - 2026-09-15

### Fixed
- `TitleImage` writes the bare TGA 2.0 footer (no extension area) that retail `iconTex`/`bootTvTex`/`bootDrcTex`/`bootLogoTex` carry; it used to strip it, and the Wii U menu showed the title as a "?" tile. `Problems` now flags a missing footer and, separately, an extension or developer area. Needs TargaSharp 0.4.0.
- `MetaXml` writes `title_version` as the `unsignedInt` meta.xml declares; only app.xml's copy is hexBinary.

### Changed
- `Game.GamePadUse` is `uint?`; null leaves the base's `drc_use` alone instead of forcing 0.

## [0.7.1] - 2026-09-15

### Fixed
- `MetaXml`/`AppXml` write `title_version` (app.xml) and `region` as hexBinary; decimal values broke the Wii U menu.

## [0.7.0] - 2026-09-14

### Added
- `WiiUSharp.Nus`: `NusDownloader` fetches a title's TMD, ticket, contents and H3 tables from the update server, builds a fake ticket from a wrapped title key when the server has none, resumes finished files, and can check content 0 against the FST magic before fetching the rest. `NusUnpacker` decrypts a package back into code/content/meta with every H0–H3 and plain-content hash verified. `Tmd.Parse`, `Ticket.Parse`, `Fst.Parse`, `ContentRecord.Parse`, `EncryptedTitleKey`, `TitleKey.Encrypt`, `ContentRecord.Id` and `EncryptedSize`. Replaces NUS downloaders and CDecrypt; no keys ship.

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
