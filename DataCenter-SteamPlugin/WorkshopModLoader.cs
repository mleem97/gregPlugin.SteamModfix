using DataCenter_SteamPlugin.Configuration;
using DataCenter_SteamPlugin.Diagnostics;
using DataCenter_SteamPlugin.Integration;
using DataCenter_SteamPlugin.Sources;
using MelonLoader;

namespace DataCenter_SteamPlugin;

public sealed class WorkshopModLoader : MelonPlugin
{
    private SteamModfixConfiguration _configuration = new();
    private SourceRegistry _registry = new();
    private IMelonLoaderAdapter? _adapter;

    public override void OnEarlyInitializeMelon()
    {
        _configuration = SteamModfixRuntime.LoadConfiguration();
        if (!_configuration.Enabled) { MelonLogger.Msg("[SteamModfix] Disabled by configuration."); return; }
        _registry = SteamModfixRuntime.Discover(_configuration);
        new ConflictResolver().LogConflicts(_registry);
        _adapter = new MelonLoaderAdapterResolver().Resolve();
        LogSources();
        if (_configuration.Diagnostics.WriteSourceReport) new DiagnosticReportWriter().Write(_registry, "plugin-fallback", _adapter.AdapterId, SteamModfixRuntime.WorkshopItems, SteamModfixRuntime.SkippedItems);
        MelonLogger.Warning("[SteamModfix] Running as a normal MelonPlugin: external Plugins/UserLibs are too late for this launch. Install/call StartupBootstrap before MelonLoader.Core.Initialize for same-launch support.");
    }

    public override void OnPreModsLoaded()
    {
        if (!_configuration.Enabled || _adapter == null) return;
        if (_configuration.Loading.EnableMods && !_adapter.Register(_registry.GetRegisteredSources(MelonSourceType.Mods), includePluginsAndUserLibs: false, enableNativeLibraries: false))
            MelonLogger.Error("[SteamModfix] Mod source injection failed; local MelonLoader folders were left untouched.");
        else
            MelonLogger.Msg("[SteamModfix] External Mod directories registered before the Mod scan.");
    }

    private void LogSources()
    {
        foreach (var type in Enum.GetValues<MelonSourceType>())
        {
            MelonLogger.Msg($"[SteamModfix] Registered {type} sources:");
            foreach (var source in _registry.GetRegisteredSources(type)) MelonLogger.Msg($"  - {source.Path} ({source.Provider})");
        }
    }
}
