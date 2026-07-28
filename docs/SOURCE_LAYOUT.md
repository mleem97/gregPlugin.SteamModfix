# Source layout

The implementation is intentionally split by responsibility:

- `Configuration/` — validated JSON model and defaults;
- `Sources/` — normalized, deduplicated first-class Melon source directories;
- `Discovery/` — game-root, StreamingAssets, GregModmanager and Workshop providers;
- `Integration/` — version-checked MelonLoader adapter and duplicate diagnostics;
- `Diagnostics/` — machine-readable source report;
- `StartupBootstrap.cs` — early entry point for a preloader/manager;
- `WorkshopModLoader.cs` — normal-plugin fallback that can only inject Mods before the Mod scan.

No Workshop assembly is loaded directly by SteamModfix. The adapter adds validated folders to MelonLoader's own internal folder lists and resolver, so MelonLoader remains responsible for manifests, subfolders, dependency resolution, duplicate behavior, compatibility checks, sorting, registration and lifecycle callbacks.
