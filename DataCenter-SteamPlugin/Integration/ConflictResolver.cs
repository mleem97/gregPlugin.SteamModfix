using DataCenter_SteamPlugin.Discovery;
using DataCenter_SteamPlugin.Sources;
using MelonLoader;

namespace DataCenter_SteamPlugin.Integration;

public sealed class ConflictResolver
{
    private readonly AssemblyMetadataInspector _inspector = new();

    public void LogConflicts(SourceRegistry registry)
    {
        foreach (var type in Enum.GetValues<MelonSourceType>())
        {
            var candidates = new List<(string Path, MelonSourceDirectory Source, AssemblyMetadata Meta)>();
            foreach (var source in registry.GetRegisteredSources(type))
            {
                foreach (var dll in Directory.EnumerateFiles(source.Path, "*.dll", SearchOption.TopDirectoryOnly))
                    if (_inspector.TryInspect(dll, out var meta) && meta != null) candidates.Add((dll, source, meta));
            }
            foreach (var group in candidates.GroupBy(x => x.Meta.Name, StringComparer.OrdinalIgnoreCase))
            {
                var ordered = group.OrderBy(x => x.Source.Priority).ThenByDescending(x => x.Meta.Version).ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase).ToList();
                if (ordered.Count < 2) continue;
                MelonLogger.Warning($"[SteamModfix] Duplicate assembly '{group.Key}' detected.");
                MelonLogger.Msg($"[SteamModfix] Using {ordered[0].Source.Provider} source: {ordered[0].Path} (v{ordered[0].Meta.Version})");
                foreach (var skipped in ordered.Skip(1)) MelonLogger.Msg($"[SteamModfix] Skipping lower-priority source: {skipped.Path} (v{skipped.Meta.Version})");
            }
        }
    }
}
