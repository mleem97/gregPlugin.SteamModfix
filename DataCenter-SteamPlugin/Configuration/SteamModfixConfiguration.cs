using System.Text.Json;

namespace DataCenter_SteamPlugin.Configuration;

public sealed class SteamModfixConfiguration
{
    public bool Enabled { get; set; } = true;
    public uint AppId { get; set; }
    public SourceToggles Sources { get; set; } = new();
    public WorkshopOptions Workshop { get; set; } = new();
    public LoadingOptions Loading { get; set; } = new();
    public SecurityOptions Security { get; set; } = new();
    public DiagnosticOptions Diagnostics { get; set; } = new();

    public static SteamModfixConfiguration Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<SteamModfixConfiguration>(File.ReadAllText(path), JsonOptions);
                if (loaded != null) return loaded;
            }
        }
        catch { }
        return new SteamModfixConfiguration();
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}

public sealed class SourceToggles { public bool GameRoot { get; set; } = true; public bool StreamingAssets { get; set; } = true; public bool SteamWorkshop { get; set; } = true; }
public sealed class WorkshopOptions { public string Provider { get; set; } = "Auto"; public bool AllowLegacyLayouts { get; set; } = true; public bool AllowUnverifiedFolders { get; set; } public bool TreatRootMelonModsAsMods { get; set; } = true; }
public sealed class LoadingOptions
{
    public List<string> SourcePriority { get; set; } = new() { "GameRoot", "GregModmanager", "StreamingAssets", "SteamWorkshop" };
    public bool EnableUserLibs { get; set; } = true; public bool EnablePlugins { get; set; } = true; public bool EnableMods { get; set; } = true; public bool EnableNativeLibraries { get; set; }
}
public sealed class SecurityOptions { public bool AllowSymbolicLinks { get; set; } public bool AllowNativeLibraries { get; set; } public int MaximumWorkshopItemSizeMb { get; set; } = 1000; public int MaximumAssemblyCountPerItem { get; set; } = 500; }
public sealed class DiagnosticOptions { public bool VerboseLogging { get; set; } public bool WriteSourceReport { get; set; } = true; }
