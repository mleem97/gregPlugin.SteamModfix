using DataCenter_SteamPlugin.Configuration;
using DataCenter_SteamPlugin.Diagnostics;
using DataCenter_SteamPlugin.Integration;
using DataCenter_SteamPlugin.Sources;
using DataCenter_SteamPlugin.Staging;
using MelonLoader;
using MelonLoader.Utils;

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
        StageWorkshopDlls();
        _adapter = new MelonLoaderAdapterResolver().Resolve();
        LogSources();
        if (_configuration.Diagnostics.WriteSourceReport) new DiagnosticReportWriter().Write(_registry, "plugin-fallback", _adapter.AdapterId, SteamModfixRuntime.WorkshopItems, SteamModfixRuntime.SkippedItems);
        var pendingLate = _registry.All
            .Where(s => (s.Type == MelonSourceType.Plugins || s.Type == MelonSourceType.UserLibs)
                && s.Provider != MelonSourceProvider.GameRoot)
            .ToList();
        if (pendingLate.Count > 0)
            MelonLogger.Warning("[SteamModfix] Running as a normal MelonPlugin: external Plugins/UserLibs are too late for this launch. Install/call StartupBootstrap before MelonLoader.Core.Initialize for same-launch support.");
        else
            MelonLogger.Msg("[SteamModfix] Normal plugin mode; no external Plugins/UserLibs pending for this launch.");
    }

    public override void OnPreModsLoaded()
    {
        if (!_configuration.Enabled || _adapter == null) return;
        if (_configuration.Loading.EnableMods && !_adapter.Register(_registry.GetRegisteredSources(MelonSourceType.Mods), includePluginsAndUserLibs: false, enableNativeLibraries: false))
            MelonLogger.Error("[SteamModfix] Mod source injection failed; local MelonLoader folders were left untouched.");
        else
            MelonLogger.Msg("[SteamModfix] External Mod directories registered before the Mod scan.");
    }

    /// <summary>
    /// Mirrors each Workshop item's content tree into the game directory
    /// (Mods/Plugins/UserLibs/UserData) so they load like local mods. Runs
    /// before the Mod scan. Only the mirror folders are touched — models and
    /// other files stay in the Workshop folder for the game's mod system.
    /// </summary>
    private void StageWorkshopDlls()
    {
        try
        {
            var result = new WorkshopContentStager().Stage(_registry, _configuration,
                MelonEnvironment.GameRootDirectory, m => MelonLogger.Msg(m));
            MelonLogger.Msg($"[SteamModfix] Workshop content staging: {result.Copied} copied, " +
                $"{result.UpToDate} up to date, {result.Pruned} pruned, " +
                $"{result.SkippedNotMod} non-mod skipped, {result.Errors} errors.");
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[SteamModfix] Workshop content staging failed: " + ex.Message);
        }
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
