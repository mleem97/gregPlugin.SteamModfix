using MelonLoader.Utils;

namespace DataCenter_SteamPlugin.Discovery;

public sealed class SteamLibraryLocator
{
    public IReadOnlyList<string> FindLibraries()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var gameRoot = Path.GetFullPath(MelonEnvironment.GameRootDirectory);
        var steamApps = Directory.GetParent(gameRoot)?.Parent?.FullName;
        if (!string.IsNullOrWhiteSpace(steamApps))
        {
            var libraryRoot = Directory.GetParent(steamApps)?.FullName;
            if (libraryRoot != null) result.Add(libraryRoot);
            AddVdfLibraries(Path.Combine(steamApps, "libraryfolders.vdf"), result);
        }
        var env = Environment.GetEnvironmentVariable("STEAM_LIBRARY_PATHS");
        if (!string.IsNullOrWhiteSpace(env)) foreach (var p in env.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)) result.Add(p.Trim());
        return result.Where(Directory.Exists).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void AddVdfLibraries(string path, HashSet<string> result)
    {
        try
        {
            if (!File.Exists(path)) return;
            foreach (var line in File.ReadLines(path))
            {
                var parts = line.Split('\"', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length >= 2 && (parts[0] == "path" || parts[^2] == "path")) result.Add(parts[^1].Replace("\\\\", "\\"));
            }
        }
        catch { }
    }
}
