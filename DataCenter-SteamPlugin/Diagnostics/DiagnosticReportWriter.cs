using System.Text.Json;
using DataCenter_SteamPlugin.Sources;
using MelonLoader.Utils;

namespace DataCenter_SteamPlugin.Diagnostics;

public sealed class DiagnosticReportWriter
{
    public void Write(SourceRegistry registry, string mode, string adapter, int workshopItems, int skippedItems)
    {
        var report = new
        {
            generatedUtc = DateTime.UtcNow,
            gameRoot = MelonEnvironment.GameRootDirectory,
            melonLoaderVersion = typeof(MelonLoader.MelonBase).Assembly.GetName().Version?.ToString(),
            mode,
            adapter,
            sameLaunchPluginLoading = mode == "early-bootstrap",
            sources = Enum.GetValues<MelonSourceType>().ToDictionary(t => t.ToString(), t => registry.GetRegisteredSources(t)),
            workshopItems,
            skippedWorkshopItems = skippedItems,
        };
        var path = Path.Combine(MelonEnvironment.UserDataDirectory, "gregPlugin.SteamModfix", "source-report.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }
}
