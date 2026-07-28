namespace DataCenter_SteamPlugin.Discovery;

public sealed record WorkshopItemInfo(string Id, string Path, bool Installed, bool NeedsUpdate, bool Downloading);

public sealed class WorkshopMetadataReader
{
    public IReadOnlyList<WorkshopItemInfo> Read(string library, uint appId, bool allowUnverifiedFolders)
    {
        var root = Path.Combine(library, "steamapps", "workshop", "content", appId.ToString());
        var acf = Path.Combine(library, "steamapps", $"appworkshop_{appId}.acf");
        var subscribed = ParseSubscribedIds(acf);
        if (!Directory.Exists(root)) return Array.Empty<WorkshopItemInfo>();
        var result = new List<WorkshopItemInfo>();
        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            var id = Path.GetFileName(dir);
            if (!subscribed.Contains(id) && !allowUnverifiedFolders) continue;
            bool installed = true;
            bool update = false;
            result.Add(new WorkshopItemInfo(id, Path.GetFullPath(dir), installed, update, false));
        }
        return result;
    }

    private static HashSet<string> ParseSubscribedIds(string path)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(path)) return result;
            foreach (var line in File.ReadLines(path))
            {
                var parts = line.Split('\"', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length < 2) continue;
                if (ulong.TryParse(parts[0], out _)) result.Add(parts[0]);
                else if (ulong.TryParse(parts[1], out _)) result.Add(parts[1]);
            }
        }
        catch { }
        return result;
    }
}
