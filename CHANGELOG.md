# Changelog — gregPlugin.SteamModfix

Format: [Keep a Changelog](https://keepachangelog.com/de/1.0.0/). Version: siehe [`VERSION`](VERSION).

## [Unreleased]

### Fixed

- Workshop items with `content/` wrapper layout (`content/Mods`, `content/Plugins`, …) are now detected and staged (`Mods/X.dll` instead of skipped or `Mods/content/Mods/X.dll`); source registration covers `content/` too; `UserData` added to mirror markers. Applies to legacy flat DLLs inside `content/` as well.

### Added

- Einheitliches Open-Source-Layout (README, Docs, Badges) nach gregCore-Vorbild.

## [0.1.0] — 2026-09-22

- Initialer standardisierter Stand.
