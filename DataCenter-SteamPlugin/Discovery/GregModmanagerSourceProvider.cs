using DataCenter_SteamPlugin.Configuration;
using DataCenter_SteamPlugin.Sources;

namespace DataCenter_SteamPlugin.Discovery;

/// <summary>Bridge for GregModmanager-managed installations without coupling this library to its process.</summary>
public sealed class GregModmanagerSourceProvider
{
    public void Discover(SourceRegistry registry, SteamModfixConfiguration config)
    {
        var raw = Environment.GetEnvironmentVariable("GREGMODMANAGER_SOURCES");
        if (string.IsNullOrWhiteSpace(raw)) return;
        var priority = SourcePriority.For(config, MelonSourceProvider.GregModmanager);
        foreach (var root in raw.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            Register(registry, Path.Combine(root, "UserLibs"), MelonSourceType.UserLibs, root + ":userlibs", priority);
            Register(registry, Path.Combine(root, "Plugins"), MelonSourceType.Plugins, root + ":plugins", priority);
            Register(registry, Path.Combine(root, "Mods"), MelonSourceType.Mods, root + ":mods", priority);
        }
    }
    private static void Register(SourceRegistry r, string path, MelonSourceType type, string id, int priority) => r.RegisterSource(new MelonSourceDirectory { Path = path, Type = type, Provider = MelonSourceProvider.GregModmanager, SourceId = id, Priority = priority, ReadOnly = true });
}
