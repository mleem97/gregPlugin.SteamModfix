namespace DataCenter_SteamPlugin.Sources;

public enum MelonSourceType { UserLibs, Plugins, Mods }
public enum MelonSourceProvider { GameRoot, GregModmanager, StreamingAssets, SteamWorkshop }

public sealed class MelonSourceDirectory
{
    public string Path { get; init; } = string.Empty;
    public MelonSourceType Type { get; init; }
    public MelonSourceProvider Provider { get; init; }
    public string SourceId { get; init; } = string.Empty;
    public int Priority { get; init; }
    public bool ReadOnly { get; init; }
}

public sealed class SourceRegistry
{
    private readonly Dictionary<MelonSourceType, Dictionary<string, MelonSourceDirectory>> _sources = new();
    public SourceRegistry() { foreach (var t in Enum.GetValues<MelonSourceType>()) _sources[t] = new(StringComparer.OrdinalIgnoreCase); }

    public bool RegisterSource(MelonSourceDirectory source)
    {
        var canonical = Canonicalize(source.Path);
        if (canonical == null || !Directory.Exists(canonical)) return false;
        _sources[source.Type][canonical] = new MelonSourceDirectory { Path = canonical, Type = source.Type, Provider = source.Provider, SourceId = source.SourceId, Priority = source.Priority, ReadOnly = source.ReadOnly };
        return true;
    }

    public bool RegisterSearchDirectory(string path, int priority) => RegisterSource(new MelonSourceDirectory { Path = path, Type = MelonSourceType.UserLibs, Provider = MelonSourceProvider.GameRoot, SourceId = "resolver", Priority = priority, ReadOnly = true });
    public IReadOnlyList<MelonSourceDirectory> GetRegisteredSources(MelonSourceType type) => _sources[type].Values.OrderBy(x => x.Priority).ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase).ToList();
    public IReadOnlyList<MelonSourceDirectory> All => _sources.Values.SelectMany(x => x.Values).OrderBy(x => x.Priority).ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase).ToList();

    public static string? Canonicalize(string path)
    {
        try { if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return null; return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
        catch { return null; }
    }
}
