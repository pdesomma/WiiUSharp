# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

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
