using DataCenter_SteamPlugin.Configuration;

namespace DataCenter_SteamPlugin.Sources;

public static class SourcePriority
{
    public static int For(SteamModfixConfiguration config, MelonSourceProvider provider)
    {
        var name = provider.ToString();
        var index = config.Loading.SourcePriority.FindIndex(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index * 10 : 100;
    }
}
