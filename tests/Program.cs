using DataCenter_SteamPlugin.Configuration;
using DataCenter_SteamPlugin.Discovery;
using DataCenter_SteamPlugin.Sources;
using DataCenter_SteamPlugin.Staging;

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
    RunStagingTests(root);
    Console.WriteLine("SteamModfix tests passed.");
}
finally { Directory.Delete(root, true); }

static void Assert(bool value, string name)
{
    if (!value) throw new InvalidOperationException("FAILED: " + name);
}

static void RunStagingTests(string root)
{
    var gameRoot = Path.Combine(root, "Game");
    var itemRoot = Path.Combine(root, "workshop", "content", "4170200", "12345");
    Directory.CreateDirectory(gameRoot);
    Directory.CreateDirectory(itemRoot);

    // === Workshop item = 1:1 mirror of the game directory ===
    // Mods: a mod DLL + junk that must never be staged.
    Directory.CreateDirectory(Path.Combine(itemRoot, "Mods"));
    var modDll = Path.Combine(itemRoot, "Mods", "FakeMod.dll");
    File.WriteAllBytes(modDll, new byte[] { 0x4D, 0x5A, 0x01 });
    File.WriteAllText(Path.Combine(itemRoot, "Mods", "model.fbx"), "fake-model");
    File.WriteAllText(Path.Combine(itemRoot, "Mods", "readme.txt"), "hi");
    File.WriteAllText(Path.Combine(itemRoot, "Mods", "broken.dll"), "not a PE file");

    // Plugins: a plugin DLL.
    Directory.CreateDirectory(Path.Combine(itemRoot, "Plugins"));
    var pluginDll = Path.Combine(itemRoot, "Plugins", "FakePlugin.dll");
    File.WriteAllBytes(pluginDll, new byte[] { 0x4D, 0x5A, 0x03 });

    // UserLibs: runtime dependencies shipped with the mod.
    Directory.CreateDirectory(Path.Combine(itemRoot, "UserLibs"));
    var libDll = Path.Combine(itemRoot, "UserLibs", "FakeDependency.dll");
    File.WriteAllBytes(libDll, new byte[] { 0x4D, 0x5A, 0x04 });

    // UserData: config files, nested like in the game dir.
    Directory.CreateDirectory(Path.Combine(itemRoot, "UserData", "Nested"));
    File.WriteAllText(Path.Combine(itemRoot, "UserData", "config.json"), "{}");
    File.WriteAllText(Path.Combine(itemRoot, "UserData", "Nested", "sub.cfg"), "x");

    var registry = new SourceRegistry();
    Assert(registry.RegisterSource(new MelonSourceDirectory
    {
        Path = Path.Combine(itemRoot, "Mods"), Type = MelonSourceType.Mods,
        Provider = MelonSourceProvider.SteamWorkshop, SourceId = "12345:mods", Priority = 30
    }), "register workshop mods source");
    Assert(registry.RegisterSource(new MelonSourceDirectory
    {
        Path = Path.Combine(itemRoot, "UserLibs"), Type = MelonSourceType.UserLibs,
        Provider = MelonSourceProvider.SteamWorkshop, SourceId = "12345:userlibs", Priority = 30
    }), "register workshop userlibs source");

    var config = new SteamModfixConfiguration();
    var fakeMeta = new AssemblyMetadata("FakeMod", new Version(1, 0), true, true, false);
    var pluginMeta = new AssemblyMetadata("FakePlugin", new Version(1, 0), true, false, true);
    var libMeta = new AssemblyMetadata("FakeDependency", new Version(1, 0), false, false, false);
    var stager = new WorkshopContentStager(path =>
    {
        var n = Path.GetFileName(path);
        if (string.Equals(n, "FakeMod.dll", StringComparison.OrdinalIgnoreCase)) return fakeMeta;
        if (string.Equals(n, "FakePlugin.dll", StringComparison.OrdinalIgnoreCase)) return pluginMeta;
        if (string.Equals(n, "FakeDependency.dll", StringComparison.OrdinalIgnoreCase)) return libMeta;
        return null;
    });

    var r1 = stager.Stage(registry, config, gameRoot);
    Assert(r1.Copied == 5, "stage mod dll + plugin + lib + 2 userdata files");
    Assert(r1.SkippedNotMod == 1, "skip malformed dll");
    Assert(File.Exists(Path.Combine(gameRoot, "Mods", "FakeMod.dll")), "staged mod dll exists");
    Assert(File.Exists(Path.Combine(gameRoot, "Plugins", "FakePlugin.dll")), "staged plugin exists");
    Assert(File.Exists(Path.Combine(gameRoot, "UserLibs", "FakeDependency.dll")), "staged dependency exists");
    Assert(File.Exists(Path.Combine(gameRoot, "UserData", "config.json")), "staged userdata config exists");
    Assert(File.Exists(Path.Combine(gameRoot, "UserData", "Nested", "sub.cfg")), "staged nested userdata exists");
    Assert(!File.Exists(Path.Combine(gameRoot, "Mods", "model.fbx")), "model never staged");
    Assert(!File.Exists(Path.Combine(gameRoot, "Mods", "readme.txt")), "txt never staged");
    Assert(!File.Exists(Path.Combine(gameRoot, "Mods", "broken.dll")), "broken dll never staged");
    Assert(File.Exists(Path.Combine(gameRoot, WorkshopContentStager.ManifestFileName)), "manifest written at game root");

    // Second run: everything up to date, no re-copy.
    var before = File.GetLastWriteTimeUtc(Path.Combine(gameRoot, "Mods", "FakeMod.dll"));
    var r2 = stager.Stage(registry, config, gameRoot);
    Assert(r2.Copied == 0 && r2.UpToDate == 5, "second run up to date");
    Assert(File.GetLastWriteTimeUtc(Path.Combine(gameRoot, "Mods", "FakeMod.dll")) == before, "no re-copy when unchanged");

    // User-owned file (not in manifest) is never deleted by prune.
    var userDll = Path.Combine(gameRoot, "Mods", "UserMod.dll");
    File.WriteAllBytes(userDll, new byte[] { 0x4D, 0x5A, 0x02 });

    // Unsubscribed item: source gone -> staged copies pruned, user file kept.
    Directory.Delete(Path.Combine(root, "workshop"), true);
    var r3 = stager.Stage(registry, config, gameRoot);
    Assert(r3.Pruned == 5, "prune unsubscribed item");
    Assert(!File.Exists(Path.Combine(gameRoot, "Mods", "FakeMod.dll")), "staged mod removed");
    Assert(!File.Exists(Path.Combine(gameRoot, "Plugins", "FakePlugin.dll")), "staged plugin removed");
    Assert(!File.Exists(Path.Combine(gameRoot, "UserLibs", "FakeDependency.dll")), "staged dep removed");
    Assert(!File.Exists(Path.Combine(gameRoot, "UserData", "config.json")), "staged userdata removed");
    Assert(File.Exists(userDll), "user file untouched");

    // Disabled staging does nothing.
    config.Staging.EnableDllStaging = false;
    var r4 = stager.Stage(registry, config, gameRoot);
    Assert(r4.Scanned == 0 && r4.Copied == 0, "disabled staging is a no-op");
}
