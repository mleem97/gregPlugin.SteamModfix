using System.Reflection;
using DataCenter_SteamPlugin.Sources;
using MelonLoader;
using MelonLoader.Melons;
using MelonLoader.Resolver;

namespace DataCenter_SteamPlugin.Integration;

public interface IMelonLoaderAdapter
{
    string AdapterId { get; }
    bool IsSupported { get; }
    bool Register(IReadOnlyCollection<MelonSourceDirectory> sources, bool includePluginsAndUserLibs, bool enableNativeLibraries);
}

public sealed class MelonLoaderAdapter_0_7_3 : IMelonLoaderAdapter
{
    public string AdapterId => "MelonLoaderAdapter_0_7_3";
    public bool IsSupported { get; }
    private readonly FieldInfo? _userLibDirs;
    private readonly FieldInfo? _pluginDirs;
    private readonly FieldInfo? _modDirs;

    public MelonLoaderAdapter_0_7_3()
    {
        var asm = typeof(MelonFolderHandler).Assembly;
        IsSupported = asm.GetName().Version?.Major == 0 && asm.GetName().Version?.Minor == 7 && asm.GetName().Version?.Build == 3;
        var type = asm.GetType("MelonLoader.Melons.MelonFolderHandler");
        _userLibDirs = CheckedField(type, "_userLibDirs");
        _pluginDirs = CheckedField(type, "_pluginDirs");
        _modDirs = CheckedField(type, "_modDirs");
        IsSupported &= _userLibDirs != null && _pluginDirs != null && _modDirs != null;
    }

    public bool Register(IReadOnlyCollection<MelonSourceDirectory> sources, bool includePluginsAndUserLibs, bool enableNativeLibraries)
    {
        if (!IsSupported) { MelonLogger.Error("[SteamModfix] MelonLoader 0.7.3 adapter validation failed; external injection disabled."); return false; }
        foreach (var source in sources)
        {
            if (!includePluginsAndUserLibs && source.Type != MelonSourceType.Mods) continue;
            var field = source.Type switch { MelonSourceType.UserLibs => _userLibDirs!, MelonSourceType.Plugins => _pluginDirs!, MelonSourceType.Mods => _modDirs!, _ => null };
            if (field?.GetValue(null) is not List<string> list) { MelonLogger.Error($"[SteamModfix] Adapter field for {source.Type} has an unexpected type; injection disabled."); return false; }
            if (!list.Contains(source.Path, StringComparer.OrdinalIgnoreCase)) list.Add(source.Path);
            if (source.Type == MelonSourceType.UserLibs && enableNativeLibraries) MelonUtils.AddNativeDLLDirectory(source.Path);
            MelonAssemblyResolver.AddSearchDirectory(source.Path);
        }
        return true;
    }

    private static FieldInfo? CheckedField(Type? type, string name)
    {
        var field = type?.GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
        return field?.FieldType == typeof(List<string>) ? field : null;
    }
}

public sealed class MelonLoaderAdapterResolver
{
    public IMelonLoaderAdapter Resolve() => new MelonLoaderAdapter_0_7_3();
}
