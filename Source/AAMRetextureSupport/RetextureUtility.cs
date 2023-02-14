using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Verse;

namespace AAM.Retexture;

[HotSwapAll]
public static class RetextureUtility
{
    public static int CachedReportCount => reportCache.Count;

    private static readonly Dictionary<ThingDef, ActiveTextureReport> reportCache = new Dictionary<ThingDef, ActiveTextureReport>(128);

    /// <summary>
    /// Gets the mod that is providing the active texture for this weapon def.
    /// </summary>
    public static ModContentPack GetTextureSupplier(ThingDef weapon)
        => weapon == null ? null : GenerateTextureReport(weapon).ActiveRetextureMod;

    public static TimeSpan PreCacheAllTextureReports()
    {
        var sw = Stopwatch.StartNew();
        foreach (var weapon in DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.IsMeleeWeapon))
        {
            GenerateTextureReport(weapon);
        }
        sw.Stop();
        return sw.Elapsed;
    }

    /// <summary>
    /// Generates information about a weapon's textures, such as what mod(s) are supplying textures.
    /// </summary>
    public static ActiveTextureReport GenerateTextureReport(ThingDef weapon, bool loadFromCache = true, bool saveToCache = true)
    {
        if (weapon == null)
            return default;

        if (loadFromCache && reportCache.TryGetValue(weapon, out var found))
            return found;
        
        // 0. Get active texture.
        string texPath = ResolveTexturePath(weapon);
        if (string.IsNullOrWhiteSpace(texPath))
            return new ActiveTextureReport("Failed to find texture path i.e. graphicData.texPath");

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
        }

        if (report.AllRetextures.Count == 0)
            report.ErrorMessage = $"No textures found for path '{report.TexturePath}'";

        if (!saveToCache)
            return report;

        reportCache[weapon] = report;
        return report;
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

    [DebugAction("Advanced Animation Mod", allowedGameStates = AllowedGameStates.Entry)]
    private static void LogAllTextureReports()
    {
        var meleeWeapons = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.IsMeleeWeapon);

        TableDataGetter<ThingDef>[] table = new TableDataGetter<ThingDef>[6];
        table[0] = new TableDataGetter<ThingDef>("Def Name", d => d.defName);
        table[1] = new TableDataGetter<ThingDef>("Name", d => d.LabelCap);
        table[2] = new TableDataGetter<ThingDef>("Source", def => $"{GenerateTextureReport(def).SourceMod?.Name ?? "?"} ({GenerateTextureReport(def).SourceMod?.PackageId ?? "?"})");
        table[3] = new TableDataGetter<ThingDef>("Texture Path", def => $"{GenerateTextureReport(def).TexturePath ?? "?"}");
        table[4] = new TableDataGetter<ThingDef>("Texture Source", def => $"{GenerateTextureReport(def).ActiveRetextureMod?.Name ?? "?"} ({GenerateTextureReport(def).ActiveRetextureMod?.PackageId ?? "?"})");
        table[5] = new TableDataGetter<ThingDef>("Retextures", def =>
        {
            var report = GenerateTextureReport(def);
            if (report.HasError)
                return report.ErrorMessage ?? "Error: ?";

            if (report.AllRetextures.Count <= 1)
                return "---";

            return string.Join(",\n", report.AllRetextures.Select(p => p.mod.Name));
        });

        DebugTables.MakeTablesDialog(meleeWeapons, table);
    }

    private static string ResolveTexturePath(ThingDef weapon)
    {
        string xmlPath = weapon.graphicData?.texPath.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(xmlPath))
            return null;

        // Graphic_Single is the most common and simplest scenario.
        if (weapon.graphicData.graphicClass == typeof(Graphic_Single))
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

        // Nothing was found.
        return null;
    }
}

public class HotSwapAllAttribute : Attribute { }
