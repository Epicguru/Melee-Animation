using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace AM.Retexture;

[HotSwapAll]
public static class RetextureUtility
{
    public static int CachedReportCount => reportCache.Count;
    public static IEnumerable<ActiveTextureReport> AllCachedReports => reportCache.Values;

    private static readonly Dictionary<ThingDef, ActiveTextureReport> reportCache = new Dictionary<ThingDef, ActiveTextureReport>(128);
    private static readonly Dictionary<ThingDef, bool> reportCacheIsFull = new Dictionary<ThingDef, bool>(128);
    private static readonly Dictionary<string, Func<string, int, (bool isCollection, string newPath)>> graphicClassCustomHandlers = new Dictionary<string, Func<string, int, (bool isCollection, string newPath)>>()
    {
        // AdvancedGraphics Single Randomized, is a collection despite being marked as Graphic_Single.
        {"AdvancedGraphics.Graphic_SingleRandomized", (path, pass) => pass switch
            {
                0 => (true, path),
                _ => (false, null)
            }
        },

        // AdvancedGraphics SingleQuality. The tex name has a _Quality suffix.
        // If the _Normal graphic cannot be found, the fallback is the raw xml path.
        {"AdvancedGraphics.Graphic_SingleQuality", (path, pass) => pass switch
            {
                0 => (false, path + "_Normal"),
                1 => (false, path),
                _ => (false, null)
            }
        },

    };
    private static HashSet<ModContentPack> OfficialMods;
    private static ModContentPack CoreMCP;

    /// <summary>
    /// Gets the mod that is providing the active texture for this weapon def.
    /// </summary>
    public static ModContentPack GetTextureSupplier(ThingDef weapon)
        => weapon == null ? null : GetTextureReport(weapon).ActiveRetextureMod;

    public static TimeSpan PreCacheAllTextureReports(Action<ActiveTextureReport> onReport, bool full)
    {
        var sw = Stopwatch.StartNew();
        foreach (var weapon in DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.IsMeleeWeapon))
        {
            onReport(GetTextureReport(weapon, 0, full));
        }
        sw.Stop();
        return sw.Elapsed;
    }

    /// <summary>
    /// Generates information about a weapon's textures, such as what mod(s) are supplying textures.
    /// </summary>
    public static ActiveTextureReport GetTextureReport(ThingDef weapon, int pass = 0, bool full = true, bool loadFromCache = true, bool saveToCache = true)
    {
        if (weapon == null)
            return default;

        if (loadFromCache && reportCache.TryGetValue(weapon, out var found) && reportCacheIsFull[weapon] == full)
            return found;

        // 0. Get active texture.
        string texPath = ResolveTexturePath(weapon, pass, out bool canDoAnotherPass);
        if (string.IsNullOrWhiteSpace(texPath))
            return new ActiveTextureReport($"Failed to find texture path i.e. graphicData.texPath '{weapon.graphicData.texPath}' ({weapon.graphicData.graphicClass.FullName}) pass {pass}");

        // Make basic report. Data still missing.
        var report = new ActiveTextureReport
        {
            Weapon = weapon,
            AllRetextures = new List<(ModContentPack, Texture2D)>(),
            TexturePath = texPath,
        };

        // 1. Find all texture paths.
        bool hasFirst = false;
        foreach ((ModContentPack mod, Texture2D texture, string path) in GetAllModTextures())
        {
            // If the texture path does not match, ignore it...
            if (path != texPath)
                continue;

            if (!hasFirst)
            {
                hasFirst = true;
                report.ActiveTexture = texture;
                report.ActiveRetextureMod = mod;
            }

            report.AllRetextures.Add((mod, texture));

            if (full)
                continue;
            if (saveToCache)
            {
                reportCache[weapon] = report;
                reportCacheIsFull[weapon] = false;
            }
            return report;
        }

        // 2. Check base game resources
        // Resources load, which is how base game and dlc load content...
        var resource = Resources.Load<Texture2D>($"Textures/{texPath}");
        if (resource != null)
        {
            OfficialMods ??= LoadedModManager.RunningModsListForReading.Where(m => m.IsOfficialMod).ToHashSet();
            CoreMCP ??= OfficialMods.First(m => m.PackageId == ModContentPack.CoreModPackageId);

            // If the weapon comes from a dlc, then that dlc is the texture provider.
            // Otherwise, just set the provider as one of the official mods (core or dlc).
            ModContentPack mod = weapon.modContentPack;
            if (!OfficialMods.Contains(mod))
                mod = CoreMCP;

            if (!hasFirst)
            {
                hasFirst = true;
                report.ActiveTexture = resource;
                report.ActiveRetextureMod = mod;
            }
            report.AllRetextures.Add((mod, resource));
            if (!full)
            {
                if (!saveToCache)
                    return report;
                reportCache[weapon] = report;
                reportCacheIsFull[weapon] = false;
                return report;
            }
        }

        // 3. Asset bundles scan.
        // Used by DLC as well as some weird mods.
        foreach (var pair in ScanAssetBundles(texPath))
        {
            if (!hasFirst)
            {
                hasFirst = true;
                report.ActiveTexture = pair.texture;
                report.ActiveRetextureMod = pair.mod;
            }
            report.AllRetextures.Add((pair.mod, pair.texture));

            if (full)
                continue;
            if (saveToCache)
            {
                reportCache[weapon] = report;
                reportCacheIsFull[weapon] = false;
            }
            return report;
        }

        if (report.AllRetextures.Count == 0)
        {
            report.ErrorMessage = $"No textures found for path '{report.TexturePath}' ({weapon.graphicData.graphicClass.FullName})";
            if (canDoAnotherPass)
            {
                return GetTextureReport(weapon, pass + 1, full, loadFromCache, saveToCache);
            }
        }

        if (!saveToCache)
            return report;

        reportCache[weapon] = report;
        reportCacheIsFull[weapon] = full;
        return report;
    }

    public static IEnumerable<ActiveTextureReport> GetModWeaponReports(ModContentPack mod)
    {
        if (mod == null)
            yield break;

        foreach (var pair in reportCache)
        {
            if (pair.Value.AllRetextures.Any(p => p.mod == mod))
                yield return pair.Value;
        }
    }

    private static IEnumerable<(ModContentPack mod, Texture2D texture)> ScanAssetBundles(string texPath)
    {
        var mods = LoadedModManager.RunningModsListForReading;
        string path = Path.Combine("Assets", "Data");

        for (int i = mods.Count - 1; i >= 0; i--)
        {
            string path2 = Path.Combine(path, mods[i].FolderName);
            foreach (AssetBundle assetBundle in mods[i].assetBundles.loadedAssetBundles)
            {
                string str = Path.Combine(Path.Combine(path2, GenFilePaths.ContentPath<Texture2D>()), texPath);

                foreach (string ext in ModAssetBundlesHandler.TextureExtensions)
                {
                    var loaded = assetBundle.LoadAsset<Texture2D>(str + ext);
                    if (loaded != null)
                        yield return (mods[i], loaded);
                }
            }
        }
    }

    private static IEnumerable<(ModContentPack mod, Texture2D texture, string path)> GetAllModTextures()
    {
        var mods = LoadedModManager.RunningModsListForReading;
        for (int i = mods.Count - 1; i >= 0; i--)
        {
            var mod = mods[i];

            var textures = mod.textures?.contentList;
            if (textures == null)
                continue;

            foreach (var pair in textures)
            {
                yield return (mod, pair.Value, pair.Key);
            }
        }

    }

    [DebugOutput("Melee Animation")]
    private static void LogAllTextureReports()
    {
        var meleeWeapons = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.IsMeleeWeapon);

        TableDataGetter<ThingDef>[] table = new TableDataGetter<ThingDef>[6];
        table[0] = new TableDataGetter<ThingDef>("Def Name", d => d.defName);
        table[1] = new TableDataGetter<ThingDef>("Name", d => d.LabelCap);
        table[2] = new TableDataGetter<ThingDef>("Source", def => $"{GetTextureReport(def).SourceMod?.Name ?? "?"} ({GetTextureReport(def).SourceMod?.PackageId ?? "?"})");
        table[3] = new TableDataGetter<ThingDef>("Texture Path", def => $"{GetTextureReport(def).TexturePath ?? "?"}");
        table[4] = new TableDataGetter<ThingDef>("Texture Source", def => $"{GetTextureReport(def).ActiveRetextureMod?.Name ?? "?"} ({GetTextureReport(def).ActiveRetextureMod?.PackageId ?? "?"})");
        table[5] = new TableDataGetter<ThingDef>("Retextures", def =>
        {
            var report = GetTextureReport(def);
            if (report.HasError)
                return report.ErrorMessage ?? "Error: ?";

            if (report.AllRetextures.Count <= 1)
                return "---";

            return string.Join(",\n", report.AllRetextures.Select(p => p.mod.Name));
        });

        DebugTables.MakeTablesDialog(meleeWeapons, table);
    }

    private static string ResolveTexturePath(ThingDef weapon, int pass, out bool canDoAnotherPass)
    {
        canDoAnotherPass = false;
        string xmlPath = weapon.graphicData?.texPath.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(xmlPath))
            return null;

        var gc = weapon.graphicData.graphicClass;
        bool isCollection = gc.IsSubclassOf(typeof(Graphic_Collection));

        // Attempt to get custom handler for this graphic class (required for some modded classes)
        // and override the xmlpath and/or collection status.
        if (graphicClassCustomHandlers.TryGetValue(gc.FullName, out var func))
        {
            (isCollection, xmlPath) = func(xmlPath, pass);
            canDoAnotherPass = xmlPath != null;
        }

        // Collections load from sub-folders, normal graphic types don't:
        if (!isCollection)
            return xmlPath;

        // Other graphic classes will pull images from a sub-folder.
        // Scan mod in reverse load order and attempt to find a subfolder with the correct path.
        var mods = LoadedModManager.RunningModsListForReading;
        for (int i = mods.Count - 1; i >= 0; i--)
        {
            var mod = mods[i];

            var textures = mod.textures?.contentListTrie;
            if (textures == null)
                continue;

            string prefix = (xmlPath[xmlPath.Length - 1] == '/') ? xmlPath : xmlPath + "/";
            foreach (string path in textures.GetByPrefix(prefix).OrderBy(s => s))
            {
                return path;
            }
        }

        // If no mods have it, check vanilla resources.
        var inFolder = Resources.LoadAll<Texture2D>($"Textures/{xmlPath}");
        if (inFolder is { Length: > 0 })
        {
            string name = inFolder.OrderBy(t => t.name).FirstOrDefault().name;
            Resources.UnloadUnusedAssets();
            return $"{xmlPath}/{name}";
        }

        // Nothing was found.
        return null;
    }
}

public class HotSwapAllAttribute : Attribute { }
