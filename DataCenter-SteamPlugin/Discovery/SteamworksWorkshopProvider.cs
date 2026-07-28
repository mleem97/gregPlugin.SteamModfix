using System.Reflection;
using MelonLoader;

namespace DataCenter_SteamPlugin.Discovery;

/// <summary>Optional Steamworks.NET adapter. It is reflection-only so SteamModfix remains usable without Steamworks.NET.</summary>
public sealed class SteamworksWorkshopProvider
{
    public bool TryEnumerate(uint appId, out IReadOnlyList<WorkshopItemInfo> items)
    {
        items = Array.Empty<WorkshopItemInfo>();
        try
        {
            var ugc = Type.GetType("Steamworks.SteamUGC, Steamworks.NET") ?? Type.GetType("Steamworks.SteamUGC, Steamworks");
            if (ugc == null) return false;
            var count = Convert.ToUInt32(ugc.GetMethod("GetNumSubscribedItems", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null));
            var getItems = ugc.GetMethod("GetSubscribedItems", BindingFlags.Public | BindingFlags.Static);
            var idType = getItems?.GetParameters().FirstOrDefault()?.ParameterType.GetElementType();
            if (getItems == null || idType == null) return false;
            var ids = Array.CreateInstance(idType, count);
            getItems.Invoke(null, new object[] { ids, count });
            var result = new List<WorkshopItemInfo>();
            foreach (var id in ids)
            {
                var stateValue = Convert.ToInt32(ugc.GetMethod("GetItemState")?.Invoke(null, new[] { id }));
                var installed = (stateValue & 4) != 0;
                var needsUpdate = (stateValue & 8) != 0;
                var downloading = (stateValue & 16) != 0 || (stateValue & 32) != 0;
                if (!TryGetInstallPath(ugc, id, out var path)) continue;
                result.Add(new WorkshopItemInfo(id?.ToString() ?? string.Empty, path!, installed, needsUpdate, downloading));
            }
            items = result;
            MelonLogger.Msg($"[SteamModfix] Steamworks provider found {result.Count} subscribed Workshop item(s).");
            return true;
        }
        catch (Exception ex) { MelonLogger.Warning($"[SteamModfix] Steamworks provider unavailable ({ex.GetType().Name}); using ACF fallback."); return false; }
    }

    private static bool TryGetInstallPath(Type ugc, object id, out string? path)
    {
        path = null;
        var method = ugc.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(x => x.Name == "GetItemInstallInfo" && x.GetParameters().Length >= 4);
        if (method == null) return false;
        var args = new object?[] { id, (ulong)0, null, (uint)4096, (uint)0 };
        var ok = Convert.ToBoolean(method.Invoke(null, args));
        path = args.Skip(1).OfType<string>().FirstOrDefault();
        return ok && !string.IsNullOrWhiteSpace(path);
    }
}
