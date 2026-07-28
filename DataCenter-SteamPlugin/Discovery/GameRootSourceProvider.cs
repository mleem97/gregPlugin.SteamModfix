using DataCenter_SteamPlugin.Configuration;
using DataCenter_SteamPlugin.Sources;
using MelonLoader.Utils;

namespace DataCenter_SteamPlugin.Discovery;

public sealed class GameRootSourceProvider
{
    public void Discover(SourceRegistry registry, SteamModfixConfiguration config)
    {
        if (!config.Sources.GameRoot) return;
        var priority = SourcePriority.For(config, MelonSourceProvider.GameRoot);
        Register(registry, MelonEnvironment.UserLibsDirectory, MelonSourceType.UserLibs, "game-root-userlibs", priority);
        Register(registry, MelonEnvironment.PluginsDirectory, MelonSourceType.Plugins, "game-root-plugins", priority);
        Register(registry, MelonEnvironment.ModsDirectory, MelonSourceType.Mods, "game-root-mods", priority);
    }

    private static void Register(SourceRegistry r, string path, MelonSourceType type, string id, int priority) => r.RegisterSource(new MelonSourceDirectory { Path = path, Type = type, Provider = MelonSourceProvider.GameRoot, SourceId = id, Priority = priority, ReadOnly = false });
}
