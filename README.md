# gregPlugin.SteamModfix

> External MelonLoader source integration for Data Center: GameRoot, StreamingAssets, GregModmanager and installed Steam Workshop items.

[![Discord Members](https://img.shields.io/discord/1392073682133848075?style=for-the-badge&logo=discord&logoColor=white&label=Discord%20Members)](https://discord.gg/greg)
[![gregFramework](https://img.shields.io/badge/gregFramework-Website-blue?style=for-the-badge)](https://gregframework.eu)
[![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](./LICENSE)
[![Version](https://img.shields.io/badge/Version-2.0.0-orange?style=for-the-badge)](https://github.com/mleem97/gregPlugin.SteamModfix/releases/tag/v2.0.0)
[![GameVersion](https://img.shields.io/badge/Game%20Version-1.1.0-yellow?style=for-the-badge)]()
[![Unity](https://img.shields.io/badge/Unity-6000.4.12f1-black?style=for-the-badge&logo=unity&logoColor=white)]()

## Links

- **Repository:** [github.com/mleem97/gregPlugin.SteamModfix](https://github.com/mleem97/gregPlugin.SteamModfix)
- **Release:** [v2.0.0](https://github.com/mleem97/gregPlugin.SteamModfix/releases/tag/v2.0.0)
- **Discord / Support:** [discord.gg/greg](https://discord.gg/greg)
- **Website:** [gregframework.eu](https://gregframework.eu)

## Features

- Registers external MelonLoader source directories without copying or modifying Workshop files.
- Supports GameRoot `Mods`, `Plugins`, `UserLibs`.
- Supports dynamic Unity `*_Data/StreamingAssets` and legacy `StreamingAssets/MelonLoader` layouts.
- Discovers installed and subscribed Workshop items through optional Steamworks.NET reflection or read-only `appworkshop_<AppId>.acf` fallback.
- Supports `Mods`, `Plugins`, `UserLibs`, `MelonLoader/...`, and metadata-inspected legacy Workshop root DLLs.
- Uses MelonLoader's own folder preprocessing, dependency resolution, duplicate handling, sorting, registration and lifecycle pipeline.
- Provides source priorities, conflict diagnostics, path validation, symlink protection and machine-readable reports.
- Includes a validated `MelonLoaderAdapter_0_7_3` and an early bootstrap API for GregModmanager.

## Compatibility and startup timing

Tested with Data Center 1.1.0, Unity 6000.4.12f1, MelonLoader 0.7.3 and x64.

MelonLoader scans UserLibs and Plugins before normal MelonPlugins are initialized. Therefore the ordinary plugin can only register external Mods during `OnPreModsLoaded`. Same-launch Workshop Plugins and UserLibs require a preloader or GregModmanager to call:

```csharp
new StartupBootstrap().RegisterBeforeFolderScan();
```

This call must happen before `MelonLoader.Core.Initialize()` invokes `ScanForFolders()`. The plugin does not falsely claim same-launch Plugin/UserLib support when installed as a normal plugin.

## Installation

1. Install MelonLoader 0.7.3 for Data Center.
2. Download `gregPlugin.SteamModfix.dll` from the [v2.0.0 release](https://github.com/mleem97/gregPlugin.SteamModfix/releases/tag/v2.0.0).
3. Copy it to `Data Center/Plugins/` or `Data Center/Mods/`.
4. For same-launch external Plugins/UserLibs, integrate `StartupBootstrap` into the pre-launch bootstrap path.
5. Start the game and inspect `MelonLoader/Latest.log` and `UserData/gregPlugin.SteamModfix/source-report.json`.

Workshop directories remain controlled by Steam and are never moved, overwritten or deleted.

## Configuration

The configuration file is `UserData/gregPlugin.SteamModfix/config.json`:

```json
{
  "enabled": true,
  "appId": 0,
  "sources": { "gameRoot": true, "streamingAssets": true, "steamWorkshop": true },
  "workshop": { "provider": "Auto", "allowLegacyLayouts": true, "allowUnverifiedFolders": false, "treatRootMelonModsAsMods": true },
  "loading": {
    "sourcePriority": ["GameRoot", "GregModmanager", "StreamingAssets", "SteamWorkshop"],
    "enableUserLibs": true, "enablePlugins": true, "enableMods": true, "enableNativeLibraries": false
  },
  "security": { "allowSymbolicLinks": false, "allowNativeLibraries": false, "maximumWorkshopItemSizeMb": 1000, "maximumAssemblyCountPerItem": 500 },
  "diagnostics": { "verboseLogging": false, "writeSourceReport": true }
}
```

`appId: 0` enables automatic detection. `GREGMODMANAGER_SOURCES` can contain additional manager source roots separated by the platform path separator.

## Build from Source

Requirements:

- .NET 6 SDK/runtime
- Data Center 1.1.0 / Unity 6000.4.12f1
- MelonLoader 0.7.3 reference assemblies in `DataCenter-SteamPlugin/references/` (intentionally not committed)

```bash
dotnet restore DataCenter-SteamPlugin/DataCenter-SteamPlugin.csproj
dotnet build DataCenter-SteamPlugin/DataCenter-SteamPlugin.csproj -c Release --no-restore
dotnet run --project tests/SteamModfix.Tests.csproj -c Release
```

Build output:

```text
DataCenter-SteamPlugin/bin/Release/net6.0/gregPlugin.SteamModfix.dll
```

The public GitHub release contains the built DLL. Generated `bin/`, `obj/`, and private reference assemblies are excluded from Git.

## Project Structure

```text
DataCenter-SteamPlugin/
├── Configuration/       # JSON configuration model
├── Discovery/           # GameRoot, StreamingAssets, manager and Workshop providers
├── Integration/         # MelonLoader adapter and conflict resolver
├── Sources/             # normalized source abstraction and priorities
├── Diagnostics/         # source-report writer
├── StartupBootstrap.cs  # pre-scan entry point
└── WorkshopModLoader.cs # normal-plugin Mods fallback
tests/                   # deterministic source/configuration/metadata tests
docs/                    # source layout and maintenance notes
```

## License

MIT. See [LICENSE](./LICENSE).

## Join the gregFramework Team!

Testing, documentation and feedback are welcome in the [greg Discord](https://discord.gg/greg).
