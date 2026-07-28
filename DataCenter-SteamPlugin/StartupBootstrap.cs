using DataCenter_SteamPlugin.Configuration;
using DataCenter_SteamPlugin.Diagnostics;
using DataCenter_SteamPlugin.Discovery;
using DataCenter_SteamPlugin.Integration;
using DataCenter_SteamPlugin.Sources;
using MelonLoader;
using MelonLoader.Utils;

namespace DataCenter_SteamPlugin;

/// <summary>
/// Entry point for GregModmanager or a maintained MelonLoader preloader.
/// It must be called before MelonLoader.Core.Initialize() invokes ScanForFolders.
/// </summary>
public sealed class StartupBootstrap
{
    public bool RegisterBeforeFolderScan(string? configPath = null)
    {
        var config = SteamModfixRuntime.LoadConfiguration(configPath);
        if (!config.Enabled) return false;
        var registry = SteamModfixRuntime.Discover(config);
        new ConflictResolver().LogConflicts(registry);
        var adapter = new MelonLoaderAdapterResolver().Resolve();
        if (!adapter.IsSupported) { MelonLogger.Error($"[SteamModfix] Unsupported MelonLoader version {typeof(MelonBase).Assembly.GetName().Version}; external source injection disabled."); return false; }
        var ok = adapter.Register(registry.All, includePluginsAndUserLibs: true, config.Loading.EnableNativeLibraries && config.Security.AllowNativeLibraries);
        if (config.Diagnostics.WriteSourceReport) new DiagnosticReportWriter().Write(registry, "early-bootstrap", adapter.AdapterId, SteamModfixRuntime.WorkshopItems, SteamModfixRuntime.SkippedItems);
        return ok;
    }
}

internal static class SteamModfixRuntime
{
    internal static int WorkshopItems { get; private set; }
    internal static int SkippedItems { get; private set; }
    internal static SteamModfixConfiguration LoadConfiguration(string? explicitPath = null) => SteamModfixConfiguration.Load(explicitPath ?? Path.Combine(MelonEnvironment.UserDataDirectory, "gregPlugin.SteamModfix", "config.json"));
    internal static SourceRegistry Discover(SteamModfixConfiguration config)
    {
        var registry = new SourceRegistry();
        new GameRootSourceProvider().Discover(registry, config);
        new GregModmanagerSourceProvider().Discover(registry, config);
        new StreamingAssetsSourceProvider().Discover(registry, config);
        var workshop = new SteamWorkshopSourceProvider(); workshop.Discover(registry, config); WorkshopItems = workshop.DiscoveredItems; SkippedItems = workshop.SkippedItems;
        return registry;
    }
}
