using System.Text.Json;
using DataCenter_SteamPlugin.Configuration;
using DataCenter_SteamPlugin.Discovery;
using DataCenter_SteamPlugin.Sources;

namespace DataCenter_SteamPlugin.Staging;

/// <summary>
/// Mirrors each Steam Workshop item's content tree into the game directory.
///
/// A Workshop mod project's content folder is a 1:1 mirror of the game
/// directory: "Mods" goes to Mods, "Plugins" to Plugins, "UserLibs" to
/// UserLibs and "UserData" to UserData (recursively). Items that follow this
/// layout are moved into place automatically, so dependencies packed into
/// "content/UserLibs" are distributed with their mod.
///
/// Legacy items (MelonMod DLLs directly in the item root) are still handled:
/// they are staged into the Mods folder like before.
///
/// Safety rules:
/// - Mods: only assemblies that actually contain a MelonMod subclass are staged.
/// - Plugins: only assemblies that actually contain a MelonPlugin subclass.
/// - UserLibs: any DLL is staged (they are libraries, not mods).
/// - UserData: every file is staged recursively (configs, save data, ...).
/// - Files that are not relevant (models, readmes, ...) live in folders outside
///   the four mirror targets and are never touched.
/// - A locally present file is only overwritten when the Workshop copy is newer
///   AND (it was staged by us before OR OverwriteLocalNewer is enabled).
///   A user's own newer file is never downgraded silently.
/// - Every staged file is tracked in &lt;GameRoot&gt;/.steammodfix-staged.json. When
///   the Workshop source disappears (unsubscribed/removed) and PruneUnsubscribed
///   is enabled, our staged copy is removed again — but only if the user has not
///   replaced it in the meantime. Files we did not stage are never deleted.
/// - Everything is best-effort: any failure is counted and logged, never thrown.
/// </summary>
public sealed class WorkshopContentStager
{
    public const string ManifestFileName = ".steammodfix-staged.json";

    private static readonly HashSet<string> MirrorMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Mods", "Plugins", "UserLibs", "MelonLoader",
    };

    private readonly AssemblyMetadataInspector _inspector = new();
    private readonly Func<string, AssemblyMetadata?>? _inspectOverride;

    /// <param name="inspectOverride">Test hook: replaces PE inspection (path -> metadata or null).</param>
    public WorkshopContentStager(Func<string, AssemblyMetadata?>? inspectOverride = null)
    {
        _inspectOverride = inspectOverride;
    }

    public sealed record StageResult(
        int Scanned, int Copied, int UpToDate, int SkippedNotMod,
        int SkippedUnsafe, int Pruned, int Errors);

    /// <summary>
    /// Mirrors Workshop items into the game root. <paramref name="gameRootDir"/>
    /// is the game directory (the parent of Mods/Plugins/UserLibs/UserData).
    /// </summary>
    public StageResult Stage(SourceRegistry registry, SteamModfixConfiguration config,
        string gameRootDir, Action<string>? log = null)
    {
        int scanned = 0, copied = 0, upToDate = 0, notMod = 0, unsafeCount = 0, pruned = 0, errors = 0;
        void Log(string m) { try { log?.Invoke(m); } catch { } }

        if (!config.Staging.EnableDllStaging) return new StageResult(0, 0, 0, 0, 0, 0, 0);

        try
        {
            if (string.IsNullOrWhiteSpace(gameRootDir)) return new StageResult(0, 0, 0, 0, 0, 0, 1);
            Directory.CreateDirectory(gameRootDir);
        }
        catch (Exception ex)
        {
            Log("[SteamModfix] Content staging: game root unavailable: " + ex.Message);
            return new StageResult(0, 0, 0, 0, 0, 0, 1);
        }

        var manifest = LoadManifest(gameRootDir);

        // Derive each installed Workshop item's root from its registered sources.
        var itemRoots = new List<string>();
        foreach (var src in registry.All)
        {
            if (src.Provider != MelonSourceProvider.SteamWorkshop) continue;
            var root = FindItemRoot(src.Path);
            if (root == null || itemRoots.Contains(root)) continue;
            itemRoots.Add(root);
        }

        foreach (var itemRoot in itemRoots)
        {
            var item = DescribeItem(itemRoot);
            if (item == null) continue;
            var (itemId, targets) = item.Value;

            // Whole-item assembly budget (DLLs across all mirror folders).
            long dllCount = 0;
            foreach (var (dir, _) in targets) dllCount += CountDlls(dir);
            if (dllCount > config.Security.MaximumAssemblyCountPerItem)
            {
                Log($"[SteamModfix] Content staging: item {itemId} has {dllCount} DLLs (limit {config.Security.MaximumAssemblyCountPerItem}) — skipped.");
                continue;
            }

            foreach (var (dir, kind) in targets)
            {
                foreach (var file in EnumerateMirrorFiles(dir, kind))
                {
                    scanned++;
                    var targetRel = RelFromItem(itemRoot, file);
                    if (targetRel == null) { errors++; continue; }

                    try
                    {
                        var info = new FileInfo(file);
                        if (!config.Security.AllowSymbolicLinks && info.LinkTarget != null)
                        {
                            unsafeCount++;
                            continue;
                        }

                        if (!IsRelevant(kind, file))
                        {
                            notMod++;
                            continue;
                        }

                        var result = CopyIfNeeded(itemId, file, DestRel(kind, itemRoot, targetRel),
                            gameRootDir, config, manifest, kind);
                        switch (result)
                        {
                            case CopyOutcome.Copied: copied++; break;
                            case CopyOutcome.UpToDate: upToDate++; break;
                            case CopyOutcome.NotMod: notMod++; break;
                            case CopyOutcome.Error: errors++; break;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        Log($"[SteamModfix] Content staging: '{Path.GetFileName(file)}' failed: {ex.Message}");
                    }
                }
            }
        }

        if (config.Staging.PruneUnsubscribed)
            pruned = Prune(registry, config, gameRootDir, manifest, Log);

        SaveManifest(gameRootDir, manifest);
        return new StageResult(scanned, copied, upToDate, notMod, unsafeCount, pruned, errors);
    }

    // --- Mirror layout ------------------------------------------------------

    private enum MirrorKind { Mods, Plugins, UserLibs, UserData }

    /// <summary>
    /// Returns the item's mirror target folders (item-root relative, with kind),
    /// or null when the item has no mirror layout at all.
    /// </summary>
    private static (string ItemId, List<(string Dir, MirrorKind Kind)> Targets)? DescribeItem(string itemRoot)
    {
        try
        {
            var itemId = new DirectoryInfo(itemRoot).Name;
            var targets = new List<(string, MirrorKind)>();
            foreach (var sub in new[] { "Mods", "Plugins", "UserLibs", "UserData" })
            {
                var p = Path.Combine(itemRoot, sub);
                if (Directory.Exists(p))
                    targets.Add((p, sub switch
                    {
                        "Mods" => MirrorKind.Mods,
                        "Plugins" => MirrorKind.Plugins,
                        "UserLibs" => MirrorKind.UserLibs,
                        _ => MirrorKind.UserData,
                    }));
            }
            if (targets.Count > 0) return (itemId, targets);

            // Legacy layout: no mirror folders, but a MelonMod DLL directly in the root.
            var rootDlls = AssemblyMetadataInspector.EnumerateDlls(itemRoot).ToList();
            return rootDlls.Count > 0 ? (itemId, new List<(string, MirrorKind)> { (itemRoot, MirrorKind.Mods) }) : null;
        }
        catch { return null; }
    }

    private static long CountDlls(string dir)
    {
        try { return AssemblyMetadataInspector.EnumerateDlls(dir).LongCount(); }
        catch { return 0; }
    }

    private static IEnumerable<string> EnumerateMirrorFiles(string dir, MirrorKind kind)
    {
        // Mods/Plugins/UserLibs mirror the game's top-level folders: DLLs only.
        // UserData mirrors recursively: everything.
        if (kind == MirrorKind.UserData)
            return EnumerateAllFiles(dir);
        return AssemblyMetadataInspector.EnumerateDlls(dir);
    }

    private static IEnumerable<string> EnumerateAllFiles(string dir)
    {
        try
        {
            return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
        }
        catch { return Enumerable.Empty<string>(); }
    }

    private static string? RelFromItem(string itemRoot, string file)
    {
        try
        {
            var rootTrimmed = itemRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!file.StartsWith(rootTrimmed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return null;
            return file.Substring(rootTrimmed.Length + 1);
        }
        catch { return null; }
    }

    /// <summary>
    /// Maps an item-relative path onto a game-root-relative destination.
    /// For mirror layouts the item already carries the mirror folder
    /// ("Mods/X.dll"); for legacy flat roots it is prefixed here.
    /// </summary>
    private static string DestRel(MirrorKind kind, string itemRoot, string targetRel)
    {
        var first = targetRel.Split(new[] { '\\', '/' }, 2)[0];
        foreach (var sub in new[] { "Mods", "Plugins", "UserLibs", "UserData" })
            if (string.Equals(first, sub, StringComparison.OrdinalIgnoreCase))
                return targetRel;
        return kind switch
        {
            MirrorKind.Mods => "Mods/" + targetRel,
            MirrorKind.Plugins => "Plugins/" + targetRel,
            MirrorKind.UserLibs => "UserLibs/" + targetRel,
            MirrorKind.UserData => "UserData/" + targetRel,
            _ => targetRel,
        };
    }

    /// <summary>Whether the file belongs in the target folder at all.</summary>
    private bool IsRelevant(MirrorKind kind, string file)
    {
        if (kind is MirrorKind.UserData or MirrorKind.UserLibs) return true;
        var meta = Inspect(file);
        if (meta == null) return false;
        return kind == MirrorKind.Mods ? meta.HasMod : meta.HasPlugin;
    }

    private static string? TargetDirOf(string gameRootDir, MirrorKind kind)
    {
        var sub = kind switch
        {
            MirrorKind.Mods => "Mods",
            MirrorKind.Plugins => "Plugins",
            MirrorKind.UserLibs => "UserLibs",
            MirrorKind.UserData => "UserData",
            _ => null,
        };
        return sub == null ? null : Path.Combine(gameRootDir, sub);
    }

    private enum CopyOutcome { Copied, UpToDate, NotMod, Error }

    private CopyOutcome CopyIfNeeded(string itemId, string source, string targetRel,
        string gameRootDir, SteamModfixConfiguration config,
        Dictionary<string, StagedEntry> manifest, MirrorKind kind)
    {
        try
        {
            var targetDir = TargetDirOf(gameRootDir, kind);
            if (targetDir == null) return CopyOutcome.NotMod;
            Directory.CreateDirectory(targetDir);

            string dest = Path.Combine(targetDir, Path.GetFileName(source));
            if (kind == MirrorKind.UserData)
            {
                // Preserve the relative layout inside UserData.
                var rel = targetRel.Substring("UserData".Length).TrimStart('\\', '/');
                dest = string.IsNullOrEmpty(rel)
                    ? Path.Combine(targetDir, Path.GetFileName(source))
                    : Path.Combine(targetDir, rel);
            }

            bool tracked = manifest.ContainsKey(targetRel);
            bool destExists = File.Exists(dest);

            bool shouldCopy = !destExists;
            if (!shouldCopy)
            {
                var srcTime = File.GetLastWriteTimeUtc(source);
                var dstTime = File.GetLastWriteTimeUtc(dest);
                long srcSize, dstSize;
                try { srcSize = new FileInfo(source).Length; dstSize = new FileInfo(dest).Length; }
                catch { return CopyOutcome.Error; }

                if (srcTime > dstTime && (tracked || config.Staging.OverwriteLocalNewer))
                    shouldCopy = true;
                else if (tracked && srcSize != dstSize)
                    shouldCopy = true; // repair our own staged copy
            }

            if (!shouldCopy) return CopyOutcome.UpToDate;

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(source, dest, true);
            var staged = new FileInfo(dest);
            manifest[targetRel] = new StagedEntry
            {
                ItemId = itemId,
                SourcePath = Path.GetFullPath(source),
                SourceWriteUtcTicks = File.GetLastWriteTimeUtc(source).Ticks,
                StagedSize = staged.Length,
                StagedWriteUtcTicks = staged.LastWriteTimeUtc.Ticks,
                StagedUtc = DateTime.UtcNow.ToString("o"),
            };
            return CopyOutcome.Copied;
        }
        catch
        {
            return CopyOutcome.Error;
        }
    }

    private AssemblyMetadata? Inspect(string path)
    {
        if (_inspectOverride != null)
        {
            try { return _inspectOverride(path); }
            catch { return null; }
        }
        try { return _inspector.TryInspect(path, out var meta) ? meta : null; }
        catch { return null; }
    }

    /// <summary>
    /// Walks up from a registered source path until the folder is no longer a
    /// mirror marker (Mods/Plugins/UserLibs/MelonLoader). The result is the
    /// Workshop item root, regardless of the original layout.
    /// </summary>
    internal static string? FindItemRoot(string sourcePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sourcePath)) return null;
            var cur = Path.GetFullPath(sourcePath);
            while (true)
            {
                var leaf = Path.GetFileName(cur.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrEmpty(leaf) || !MirrorMarkers.Contains(leaf)) break;
                var parent = Directory.GetParent(cur);
                if (parent == null) break;
                cur = parent.FullName;
            }
            return cur;
        }
        catch { return null; }
    }

    private int Prune(SourceRegistry registry, SteamModfixConfiguration config,
        string gameRootDir, Dictionary<string, StagedEntry> manifest, Action<string> log)
    {
        int pruned = 0;

        var liveSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var src in registry.All)
        {
            if (src.Provider != MelonSourceProvider.SteamWorkshop) continue;
            var root = FindItemRoot(src.Path);
            if (root == null) continue;
            try
            {
                foreach (var f in EnumerateAllFiles(root)) liveSources.Add(Path.GetFullPath(f));
            }
            catch { }
        }

        var gone = new List<string>();
        foreach (var (targetRel, entry) in manifest)
        {
            bool sourceAlive;
            try { sourceAlive = File.Exists(entry.SourcePath) && liveSources.Contains(Path.GetFullPath(entry.SourcePath)); }
            catch { sourceAlive = false; }
            if (sourceAlive) continue;

            string dest = DestFromRel(gameRootDir, targetRel);
            if (dest == null) { gone.Add(targetRel); pruned++; continue; }

            try
            {
                if (File.Exists(dest))
                {
                    var current = new FileInfo(dest);
                    if (current.Length == entry.StagedSize &&
                        current.LastWriteTimeUtc.Ticks == entry.StagedWriteUtcTicks)
                    {
                        File.Delete(dest);
                        log($"[SteamModfix] Content staging: pruned unsubscribed '{targetRel}'.");
                    }
                    else
                    {
                        log($"[SteamModfix] Content staging: '{targetRel}' was replaced locally — keeping user file.");
                    }
                }
            }
            catch (Exception ex)
            {
                log($"[SteamModfix] Content staging: prune of '{targetRel}' failed: {ex.Message}");
                continue;
            }
            gone.Add(targetRel);
            pruned++;
        }
        foreach (var key in gone) manifest.Remove(key);
        return pruned;
    }

    internal static string? DestFromRel(string gameRootDir, string targetRel)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(gameRootDir) || string.IsNullOrWhiteSpace(targetRel)) return null;
            var dir = Path.GetDirectoryName(targetRel);
            var file = Path.GetFileName(targetRel);
            var target = string.IsNullOrEmpty(dir) ? Path.Combine(gameRootDir, file) : Path.Combine(gameRootDir, dir, file);
            return target;
        }
        catch { return null; }
    }

    internal sealed class StagedEntry
    {
        public string ItemId { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public long SourceWriteUtcTicks { get; set; }
        public long StagedSize { get; set; }
        public long StagedWriteUtcTicks { get; set; }
        public string StagedUtc { get; set; } = string.Empty;
    }

    private static Dictionary<string, StagedEntry> LoadManifest(string gameRootDir)
    {
        var empty = new Dictionary<string, StagedEntry>(StringComparer.OrdinalIgnoreCase);
        try
        {
            string path = ManifestPath(gameRootDir);
            if (!File.Exists(path))
            {
                // Migrate the previous Mods-only manifest (<GameRoot>/Mods/.steammodfix-staged.json).
                return MigrateLegacyManifest(gameRootDir);
            }
            var loaded = JsonSerializer.Deserialize<Dictionary<string, StagedEntry>>(File.ReadAllText(path));
            return loaded != null
                ? new Dictionary<string, StagedEntry>(loaded, StringComparer.OrdinalIgnoreCase)
                : empty;
        }
        catch { return empty; }
    }

    private static Dictionary<string, StagedEntry> MigrateLegacyManifest(string gameRootDir)
    {
        var migrated = new Dictionary<string, StagedEntry>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var legacyPath = Path.Combine(gameRootDir, "Mods", ManifestFileName);
            if (!File.Exists(legacyPath)) return migrated;
            var loaded = JsonSerializer.Deserialize<Dictionary<string, StagedEntry>>(File.ReadAllText(legacyPath));
            if (loaded == null) return migrated;
            foreach (var (fileName, entry) in loaded)
                migrated["Mods/" + fileName] = entry;
            return migrated;
        }
        catch { return migrated; }
    }

    private static void SaveManifest(string gameRootDir, Dictionary<string, StagedEntry> manifest)
    {
        try
        {
            string path = ManifestPath(gameRootDir);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(manifest,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private static string ManifestPath(string gameRootDir) => Path.Combine(gameRootDir, ManifestFileName);
}