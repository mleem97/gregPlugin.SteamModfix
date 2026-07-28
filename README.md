# gregPlugin.SteamModfix

External MelonLoader source discovery for Data Center. The rewrite keeps Workshop files read-only and registers their directories with MelonLoader's own folder scanner/resolver instead of copying DLLs into the game folders.

## Compatibility and startup timing

The implementation is tested against Data Center 1.1.0, Unity 6000.4.12f1 and MelonLoader 0.7.3 on x64.

MelonLoader 0.7.3 performs this order internally:

```text
Core.Initialize
  ScanForFolders
  load UserLibs
  load Plugins
OnPreModsLoaded
  load Mods
```

Therefore the ordinary `gregPlugin.SteamModfix.dll` can register external Mods during `OnPreModsLoaded`, but it cannot honestly provide same-launch external Plugins or UserLibs. `StartupBootstrap.RegisterBeforeFolderScan()` is the supported early entry point for GregModmanager or a maintained MelonLoader preloader. It must run before `MelonLoader.Core.Initialize()` calls `ScanForFolders()`.

If no early bootstrap is installed, the plugin logs the limitation and leaves the normal local folders untouched.

## Sources

The discovery layer handles:

- `<GameRoot>/Mods`, `Plugins` and `UserLibs`;
- every `<GameRoot>/*_Data/StreamingAssets/{Mods,Plugins,UserLibs}` and the legacy `StreamingAssets/MelonLoader/...` layout;
- installed, subscribed Workshop items found through `appworkshop_<AppId>.acf`, including `Mods`, `Plugins`, `UserLibs`, `MelonLoader/...`, and metadata-inspected legacy root DLL layouts.

Workshop items that are missing, updating, downloading, unsafe, malformed, or over the configured limits are skipped individually. Steam metadata is read-only; no Workshop file is moved, copied, deleted, or modified.

## Configuration

Configuration is stored at `UserData/gregPlugin.SteamModfix/config.json`:

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

`appId: 0` enables automatic detection from the installed Steam app manifest. The generated report is `UserData/gregPlugin.SteamModfix/source-report.json`.

## Architecture

`Discovery/` is filesystem-only and reusable by GregModmanager. `Sources/` normalizes and deduplicates source directories. `Integration/MelonLoaderAdapter_0_7_3` validates exact private field types before adding directories to MelonLoader's `_userLibDirs`, `_pluginDirs`, `_modDirs` and `MelonAssemblyResolver`. `Diagnostics/` writes reports and conflict decisions. No custom assembly loader or manual Melon registration remains.

Unsupported MelonLoader versions disable external injection and are reported clearly. The normal game-root MelonLoader folders remain unaffected.

## Build and tests

```bash
dotnet build DataCenter-SteamPlugin/DataCenter-SteamPlugin.csproj -c Release
dotnet run --project tests/SteamModfix.Tests.csproj -c Release
```

The test runner covers source normalization, missing-path rejection, deterministic priority, relative-path rejection, and configuration round-tripping.

## License

MIT. See [LICENSE](LICENSE).
