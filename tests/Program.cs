using DataCenter_SteamPlugin.Configuration;
using DataCenter_SteamPlugin.Discovery;
using DataCenter_SteamPlugin.Sources;

var root = Path.Combine(Path.GetTempPath(), "steammodfix-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var first = Path.Combine(root, "one");
    var second = Path.Combine(root, "two");
    Directory.CreateDirectory(first);
    Directory.CreateDirectory(second);
    var registry = new SourceRegistry();
    Assert(registry.RegisterSource(new MelonSourceDirectory { Path = first, Type = MelonSourceType.Mods, Provider = MelonSourceProvider.GameRoot, SourceId = "one", Priority = 0 }), "register first source");
    Assert(!registry.RegisterSource(new MelonSourceDirectory { Path = Path.Combine(first, "missing"), Type = MelonSourceType.Mods, Provider = MelonSourceProvider.SteamWorkshop, SourceId = "missing", Priority = 30 }), "reject missing source");
    Assert(registry.RegisterSource(new MelonSourceDirectory { Path = second, Type = MelonSourceType.Mods, Provider = MelonSourceProvider.SteamWorkshop, SourceId = "two", Priority = 30 }), "register second source");
    Assert(registry.GetRegisteredSources(MelonSourceType.Mods).Count == 2, "deduplicated source count");
    Assert(registry.GetRegisteredSources(MelonSourceType.Mods)[0].Provider == MelonSourceProvider.GameRoot, "source priority");
    Assert(SourceRegistry.Canonicalize("relative") == null, "reject relative path");
    var config = new SteamModfixConfiguration();
    var configPath = Path.Combine(root, "UserData", "config.json");
    config.Save(configPath);
    Assert(SteamModfixConfiguration.Load(configPath).Enabled, "configuration round trip");
    var malformed = Path.Combine(root, "malformed.dll");
    File.WriteAllText(malformed, "not a PE file");
    Assert(!new AssemblyMetadataInspector().TryInspect(malformed, out _), "reject malformed managed assembly");
    Console.WriteLine("SteamModfix tests passed.");
}
finally { Directory.Delete(root, true); }

static void Assert(bool value, string name)
{
    if (!value) throw new InvalidOperationException("FAILED: " + name);
}
