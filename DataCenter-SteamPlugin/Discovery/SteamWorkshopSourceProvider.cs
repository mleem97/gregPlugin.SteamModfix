using System.Text.Json;
using DataCenter_SteamPlugin.Configuration;
using DataCenter_SteamPlugin.Sources;
using MelonLoader;
using MelonLoader.Utils;

namespace DataCenter_SteamPlugin.Discovery;

public sealed class SteamWorkshopSourceProvider
{
    private readonly SteamLibraryLocator _libraries = new();
    private readonly WorkshopMetadataReader _metadata = new();
    private readonly AssemblyMetadataInspector _inspector = new();
    private readonly SteamworksWorkshopProvider _steamworks = new();

    public int DiscoveredItems { get; private set; }
    public int SkippedItems { get; private set; }

    public void Discover(SourceRegistry registry, SteamModfixConfiguration config)
    {
        if (!config.Sources.SteamWorkshop) return;
        var appId = config.AppId == 0 ? DetectAppId() : config.AppId;
        if (appId == 0) { MelonLogger.Warning("[SteamModfix] Steam App ID could not be detected; Workshop discovery disabled."); return; }
        if (_steamworks.TryEnumerate(appId, out var steamItems))
        {
            foreach (var item in steamItems)
            {
                if (!item.Installed || item.NeedsUpdate || item.Downloading) { SkippedItems++; continue; }
                if (!IsSafeItem(item.Path, config)) { SkippedItems++; continue; }
                DiscoveredItems++; RegisterLayout(registry, item, config);
            }
            MelonLogger.Msg($"[SteamModfix] Workshop discovery used Steamworks for AppID {appId}.");
            return;
        }
        MelonLogger.Msg($"[SteamModfix] Workshop discovery using appworkshop_{appId}.acf fallback.");
        foreach (var library in _libraries.FindLibraries())
        {
            foreach (var item in _metadata.Read(library, appId, config.Workshop.AllowUnverifiedFolders))
            {
                if (!item.Installed || item.NeedsUpdate || item.Downloading) { SkippedItems++; continue; }
                if (!IsSafeItem(item.Path, config)) { SkippedItems++; continue; }
                DiscoveredItems++;
                RegisterLayout(registry, item, config);
            }
        }
        MelonLogger.Msg($"[SteamModfix] Workshop discovery: {DiscoveredItems} installed item(s), {SkippedItems} skipped.");
    }

    private void RegisterLayout(SourceRegistry r, WorkshopItemInfo item, SteamModfixConfiguration config)
    {
        var root = item.Path;
        var priority = SourcePriority.For(config, MelonSourceProvider.SteamWorkshop);
        RegisterSet(r, root, item.Id + ":root", priority);
        RegisterSet(r, Path.Combine(root, "MelonLoader"), item.Id + ":melonloader", priority);
        if (!config.Workshop.AllowLegacyLayouts) return;
        var dlls = Directory.EnumerateFiles(root, "*.dll", SearchOption.TopDirectoryOnly).ToList();
        if (dlls.Count == 0) return;
        if (dlls.Count > config.Security.MaximumAssemblyCountPerItem) return;
        bool hasMod = false, hasPlugin = false, hasLib = false;
        foreach (var dll in dlls)
        {
            if (!_inspector.TryInspect(dll, out var meta) || meta == null) continue;
            hasMod |= meta.HasMod; hasPlugin |= meta.HasPlugin; hasLib |= !meta.HasMod && !meta.HasPlugin;
        }
        if (hasMod && config.Workshop.TreatRootMelonModsAsMods) Register(r, root, MelonSourceType.Mods, item.Id + ":legacy-mods");
        if (hasPlugin) Register(r, root, MelonSourceType.Plugins, item.Id + ":legacy-plugins");
        if (hasLib) Register(r, root, MelonSourceType.UserLibs, item.Id + ":legacy-userlibs");
    }

    private static void RegisterSet(SourceRegistry r, string root, string id, int priority)
    {
        Register(r, Path.Combine(root, "UserLibs"), MelonSourceType.UserLibs, id + ":userlibs", priority);
        Register(r, Path.Combine(root, "Plugins"), MelonSourceType.Plugins, id + ":plugins", priority);
        Register(r, Path.Combine(root, "Mods"), MelonSourceType.Mods, id + ":mods", priority);
    }
    private static void Register(SourceRegistry r, string path, MelonSourceType type, string id, int priority = 30) => r.RegisterSource(new MelonSourceDirectory { Path = path, Type = type, Provider = MelonSourceProvider.SteamWorkshop, SourceId = id, Priority = priority, ReadOnly = true });

    private static bool IsSafeItem(string path, SteamModfixConfiguration config)
    {
        try
        {
            var info = new DirectoryInfo(path);
            if (!info.Exists) return false;
            if (!config.Security.AllowSymbolicLinks && info.LinkTarget != null) return false;
            return true;
        }
        catch { return false; }
    }

    private static uint DetectAppId()
    {
        try
        {
            var root = MelonEnvironment.GameRootDirectory;
            foreach (var acf in Directory.GetFiles(Directory.GetParent(root)!.FullName, "appmanifest_*.acf"))
            {
                if (File.ReadAllText(acf).Contains($"\"installdir\"\t\"{Path.GetFileName(root)}\"", StringComparison.OrdinalIgnoreCase))
                {
                    var name = Path.GetFileNameWithoutExtension(acf).Replace("appmanifest_", "");
                    if (uint.TryParse(name, out var id)) return id;
                }
            }
        }
        catch { }
        return 0;
    }
}
