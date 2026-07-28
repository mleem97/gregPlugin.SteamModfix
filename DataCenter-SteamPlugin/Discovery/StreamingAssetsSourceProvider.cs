using DataCenter_SteamPlugin.Configuration;
using DataCenter_SteamPlugin.Sources;
using MelonLoader.Utils;

namespace DataCenter_SteamPlugin.Discovery;

public sealed class StreamingAssetsSourceProvider
{
    public void Discover(SourceRegistry registry, SteamModfixConfiguration config)
    {
        if (!config.Sources.StreamingAssets) return;
        var root = MelonEnvironment.GameRootDirectory;
        foreach (var data in Directory.EnumerateDirectories(root, "*_Data", SearchOption.TopDirectoryOnly))
        {
            var streaming = Path.Combine(data, "StreamingAssets");
            RegisterSet(registry, streaming, "streaming-assets", SourcePriority.For(config, MelonSourceProvider.StreamingAssets));
            RegisterSet(registry, Path.Combine(streaming, "MelonLoader"), "streaming-assets-melonloader", SourcePriority.For(config, MelonSourceProvider.StreamingAssets));
        }
    }

    private static void RegisterSet(SourceRegistry r, string root, string id, int priority)
    {
        Register(r, Path.Combine(root, "UserLibs"), MelonSourceType.UserLibs, id + "-userlibs", priority);
        Register(r, Path.Combine(root, "Plugins"), MelonSourceType.Plugins, id + "-plugins", priority);
        Register(r, Path.Combine(root, "Mods"), MelonSourceType.Mods, id + "-mods", priority);
    }
    private static void Register(SourceRegistry r, string path, MelonSourceType type, string id, int priority) => r.RegisterSource(new MelonSourceDirectory { Path = path, Type = type, Provider = MelonSourceProvider.StreamingAssets, SourceId = id, Priority = priority, ReadOnly = true });
}
