using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AM.Retexture;
using JetBrains.Annotations;
using LudeonTK;
using Newtonsoft.Json;
using UnityEngine;
using Verse;
using Object = UnityEngine.Object;

namespace AM.Tweaks;

public static class TweakDataManager
{
    public const string DATA_FOLDER_NAME = "WeaponTweakData";

    public static int TweakDataLoadedCount => defWithModToTweak.Count;

    private static readonly Dictionary<ThingDef, ItemTweakData> defToTweak = new Dictionary<ThingDef, ItemTweakData>();
    private static readonly Dictionary<(ThingDef def, ModContentPack mod), ItemTweakData> defWithModToTweak = new Dictionary<(ThingDef def, ModContentPack mod), ItemTweakData>();
    private static Dictionary<string, FileInfo> overrideTweakDataFile;

    /// <summary>
    /// Gets the <see cref="FileInfo"/> where the weapon tweak data is expected to exist
    /// </summary>
    public static FileInfo GetFileForTweak(ThingDef weaponDef, ModContentPack textureMod = null, FileInfo setOverride = null)
    {
        if (textureMod == null)
        {
            var report = RetextureUtility.GetTextureReport(weaponDef);
            textureMod = report.ActiveRetextureMod;
        }

        string fileName = ItemTweakData.MakeFileName(weaponDef, textureMod);

        // Allow other mods to provide tweak files.
        overrideTweakDataFile ??= MakeOverrideTweakDataFileMap();
        if (setOverride != null)
            overrideTweakDataFile[fileName] = setOverride;

        if (overrideTweakDataFile.TryGetValue(fileName, out var found))
            return found;

        string root = Core.ModContent.RootDir;
        string fp = Path.Combine(root, DATA_FOLDER_NAME, fileName);
        return new FileInfo(fp);
    }

    private static Dictionary<string, FileInfo> MakeOverrideTweakDataFileMap()
    {
        var ov = new Dictionary<string, FileInfo>();
        var mods = LoadedModManager.RunningModsListForReading;

        foreach (var mod in mods)
        {
            // Main mod does not provide overrides - every other mod takes priority over the main one.
            if (mod == Core.ModContent)
                continue;

            var folder = Path.Combine(mod.RootDir, DATA_FOLDER_NAME);
            if (!Directory.Exists(folder))
                continue;

            foreach (var file in Directory.EnumerateFiles(folder, "*.json", SearchOption.TopDirectoryOnly))
            {
                var fi = new FileInfo(file);
                ov[fi.Name] = fi;
            }
        }

        return ov;
    }

    public static int GetTweakDataFileCount(string modID)
    {
        int c = 0;
        string root = Core.ModContent.RootDir;
        modID += ".json";
        foreach (var fn in Directory.EnumerateFiles(Path.Combine(root, DATA_FOLDER_NAME), "*.json"))
        {
            if (fn.EndsWith(modID))
                c++;
        }
        return c;
    }

    public static ItemTweakData TryGetTweak(ThingDef def) => TryGetTweak(def, null);

    public static ItemTweakData TryGetTweak(ThingDef def, ModContentPack textureSupplier)
    {
        if (def == null)
            return null;

        if (textureSupplier == null)
            return defToTweak.GetValueOrDefault(def);
            
        return defWithModToTweak.GetValueOrDefault((def, textureSupplier));
    }

    public static ItemTweakData CreateNew(ThingDef def, ModContentPack textureSupplier)
    {
        var tweak = new ItemTweakData(def, textureSupplier);
        return Register(tweak) ? tweak : null;
    }

    public static ItemTweakData TryLoad(ThingDef weapon, ModContentPack textureMod)
    {
        var file = GetFileForTweak(weapon, textureMod);
        if (!file.Exists)
        {
            return null;
        }

        ItemTweakData loaded;
        try
        {
            loaded = ItemTweakData.LoadFrom(file.FullName);
        }
        catch (Exception e)
        {
            Core.Error($"Exception loading tweak data json '{file.Name}':", e);
            return null;
        }

        var existing = TryGetTweak(weapon, textureMod);
        if (existing != null)
        {
            defWithModToTweak.Remove((weapon, textureMod));
        }

        if (Register(loaded))
            return loaded;

        Core.Error("Unexpected failure to register.");
        return null;
    }

    private static bool Register(ItemTweakData td)
    {
        var def = td.GetDef();
        if (def == null)
        {
            Core.Error($"Failed to find item def for '{td.ItemDefName}' of type '{td.ItemTypeNamespace}.{td.ItemType}'. Tweak will not be registered.");
            return false;
        }

        var report = RetextureUtility.GetTextureReport(def);
        if (report.HasError)
        {
            Core.Error($"Failed to generate retexture report for {def}. Tweak will not be registered. Reason: {report.ErrorMessage}");
            return false;
        }

        var mod = report.AllRetextures.FirstOrDefault(p => ItemTweakData.MakeModID(p.mod) == td.TextureModID).mod;
        if (mod == null)
        {
            Core.Error($"Failed to find mod '{td.TextureModID}' among active retextures of {td.ItemDefName}.");
            return false;
        }

        if (defWithModToTweak.ContainsKey((def, mod)))
        {
            Core.Error($"There is already a tweak registered for def '{def}' with texture mod '{mod.Name}'");
            return false;
        }

        defToTweak[def] = td;
        defWithModToTweak.Add((def, mod), td);
        return true;
    }

    [DebugOutput("Melee Animation")]
    private static void LogAllRetextureCompletion()
    {
        var all = from td in DefDatabase<ThingDef>.AllDefsListForReading
                  where td.IsMeleeWeapon()
                  let report = RetextureUtility.GetTextureReport(td)
                  where !report.HasError
                  from pair in report.AllRetextures
                  select ( pair.mod, report );

        static TableDataGetter<(ModContentPack mod, ActiveTextureReport rep)> Row(string name, Func<(ModContentPack mod, ActiveTextureReport rep), string> toString) 
            => new TableDataGetter<(ModContentPack mod, ActiveTextureReport rep)>(name, toString);

        static TableDataGetter<(ModContentPack mod, ActiveTextureReport rep)> RowObj(string name, Func<(ModContentPack mod, ActiveTextureReport rep), object> toObj)
            => new TableDataGetter<(ModContentPack mod, ActiveTextureReport rep)>(name, toObj);

        var table = new TableDataGetter<(ModContentPack mod, ActiveTextureReport rep)>[6];
        int i = 0;
        table[i++] = Row("Def Name", p => p.rep.Weapon.defName);
        table[i++] = Row("Label", p => p.rep.Weapon.LabelCap);
        table[i++] = Row("Source", p => p.rep.SourceMod?.Name ?? "?");
        table[i++] = Row("Texture Provider", p => $"{ItemTweakData.MakeModID(p.mod)} '{p.mod.Name}'");
        table[i++] = Row("Texture Path", p => p.rep.TexturePath);
        table[i++] = RowObj("Is Saved", p => GetFileForTweak(p.rep.Weapon, p.mod).Exists.ToStringCheckBlank());

        DebugTables.MakeTablesDialog(all, table);
    }

    [DebugAction("Melee Animation", allowedGameStates = AllowedGameStates.Entry), UsedImplicitly]
    private static void OutputAllMeleeWeaponData()
    {
        // Ensure all tweak data is loaded.
        foreach (var _ in LoadAllForActiveMods(true)) { }
        
        string[] imageFileExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".tga"];
        
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string outputFolder = Path.Combine(desktop, "RimworldMeleeJsonDump");
        foreach (var weapon in DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.IsMeleeWeapon()))
        {
            var tweak = TryGetTweak(weapon);
            var texReport = RetextureUtility.GetTextureReport(weapon);
            
            var foldersToLoad = texReport.ActiveRetextureMod.foldersToLoadDescendingOrder;
            string expectedPath = null;
            
            foreach (var folder in foldersToLoad)
            {
                foreach (var ext in imageFileExtensions)
                {
                    string path = Path.Combine(folder, "Textures", $"{texReport.TexturePath}{ext}");
                    Core.Log($"Checking for texture at '{path}'");
                    if (File.Exists(path))
                    {
                        expectedPath = path;
                        break;
                    }
                }
                if (expectedPath != null)
                    break;
            }

            // Core and DLC are stored in asset bundles.
            // They need to be dumped into a folder.
            if (texReport.ActiveRetextureMod.IsCoreMod || texReport.ActiveRetextureMod.IsOfficialMod)
            {
                string savePath = Path.Combine(outputFolder, $"{weapon.defName}.png");
                var srcTex = texReport.ActiveTexture;
                
                // Create a temporary RenderTexture of the same size as the texture
                RenderTexture tmp = RenderTexture.GetTemporary( 
                    srcTex.width,
                    srcTex.height,
                    0,
                    RenderTextureFormat.Default,
                    RenderTextureReadWrite.Linear);
                
                // Blit the pixels on texture to the RenderTexture
                Graphics.Blit(srcTex, tmp);
                
                // Backup the currently set RenderTexture
                RenderTexture previous = RenderTexture.active;

                // Set the current RenderTexture to the temporary one we created
                RenderTexture.active = tmp;

                // Create a new readable Texture2D to copy the pixels to it
                Texture2D myTexture2D = new Texture2D(srcTex.width, srcTex.height);

                // Copy the pixels from the RenderTexture to the new Texture
                myTexture2D.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
                myTexture2D.Apply();

                // Reset the active RenderTexture
                RenderTexture.active = previous;
                
                // Save to file.
                byte[] dataToSave = myTexture2D.EncodeToPNG();
                File.WriteAllBytes(savePath, dataToSave);
                
                // Clean up.
                RenderTexture.ReleaseTemporary(tmp);
                Object.Destroy(myTexture2D);
                
                Core.Log($"Saved texture for {weapon.defName} to '{savePath}'.");
                expectedPath = savePath;
            }
            
            var data = new MeleeWeaponJsonDataForTweakGen
            {
                DefName = weapon.defName,
                Label = weapon.LabelCap,
                Description = weapon.DescriptionDetailed,
                TexturePath = expectedPath,
                TweakData = tweak == null ? null : new MeleeWeaponTweakDataForTweakGen(tweak)
            };
            
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            string outputPath = Path.Combine(outputFolder, $"{weapon.defName}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, json);
        }
        
        Core.Log($"Finished writing melee weapon data to '{outputFolder}'");
    }

    private sealed record MeleeWeaponJsonDataForTweakGen
    {
        public required string DefName { get; init; }
        public required string Label { get; init; }
        public required string Description { get; init; }
        public required string TexturePath { get; init; }
        [CanBeNull]
        public MeleeWeaponTweakDataForTweakGen TweakData { get; init; }
    }

    private sealed record MeleeWeaponTweakDataForTweakGen
    {
        public float Rotation { get; init; }
        public float OffsetX { get; init; }
        public float OffsetY { get; init; }
        public bool IsSharp { get; init; }
        public bool IsBlunt { get; init; }
        public bool IsStab { get; init; }
        public int HandCount { get; init; }
        public bool IsLong { get; init; }

        public MeleeWeaponTweakDataForTweakGen(ItemTweakData data)
        {
            Rotation = data.Rotation;
            OffsetX = data.OffX;
            OffsetY = data.OffY;
            IsSharp = data.MeleeWeaponType.HasFlag(MeleeWeaponType.Long_Sharp) || data.MeleeWeaponType.HasFlag(MeleeWeaponType.Short_Sharp);
            IsBlunt = data.MeleeWeaponType.HasFlag(MeleeWeaponType.Long_Blunt) || data.MeleeWeaponType.HasFlag(MeleeWeaponType.Short_Blunt);
            IsStab = data.MeleeWeaponType.HasFlag(MeleeWeaponType.Short_Stab) || data.MeleeWeaponType.HasFlag(MeleeWeaponType.Long_Stab);
            IsLong = data.MeleeWeaponType.HasFlag(MeleeWeaponType.Long_Blunt) || data.MeleeWeaponType.HasFlag(MeleeWeaponType.Long_Sharp) || data.MeleeWeaponType.HasFlag(MeleeWeaponType.Long_Stab);
            HandCount = data.HandsMode switch
            {
                HandsMode.Default => 2,
                HandsMode.Only_Main_Hand => 1,
                _ => 0
            };
        }
    }
    
    public static IEnumerable<(string modPackageID, string modName, ThingDef weapon)> LoadAllForActiveMods(bool includeRedundant)
    {
        var data = from weapon in DefDatabase<ThingDef>.AllDefsListForReading
                   where weapon.IsMeleeWeapon()
                   let report = RetextureUtility.GetTextureReport(weapon)
                   select (weapon, report);

        foreach (var pair in data)
        {
            if (pair.report.HasError)
            {
                Core.Error($"Failed to get texture report for {pair.weapon}: {pair.report.ErrorMessage}");
                continue;
            }

            // Attempt to load the tweak for the actual active retexture.
            var tweak = TryLoad(pair.weapon, pair.report.ActiveRetextureMod);
            if (tweak == null)
                yield return (ItemTweakData.MakeModID(pair.report.ActiveRetextureMod), pair.report.ActiveRetextureMod.Name, pair.weapon);

            // Fallback to other tweak data from non-active retextures.
            if (includeRedundant || tweak == null)
            {
                foreach (var retex in pair.report.AllRetextures)
                {
                    // Skip the active retexture, it has already been tried.
                    if (retex.mod == pair.report.ActiveRetextureMod)
                        continue;

                    // Try to load a tweak for this retexture...
                    tweak = TryLoad(pair.weapon, retex.mod);
                    if (tweak == null)
                        yield return (ItemTweakData.MakeModID(retex.mod), retex.mod.Name, pair.weapon);
                    else if (!includeRedundant)
                        break;
                }
            }
        }
    }

    public static IEnumerable<ModTweakContainer> GetTweaksReportForActiveMods()
    {
        var reportsByMod = from mod in LoadedModManager.RunningModsListForReading
                           orderby mod.Name
                           let reports = RetextureUtility.GetModWeaponReports(mod)
                           where reports.Any()
                           select new { mod, reports };

        foreach (var pair in reportsByMod)
        {
            yield return new ModTweakContainer
            {
                Mod = pair.mod,
                Reports = pair.reports
            };
        }
    }

    public static Texture2D ToTexture2D(this RenderTexture rTex)
    {
        Texture2D tex = new(rTex.width, rTex.height, TextureFormat.RGBA32, false);
        var oldRt = RenderTexture.active;
        RenderTexture.active = rTex;

        tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
        tex.Apply();

        RenderTexture.active = oldRt;
        return tex;
    }
}

public struct ModTweakContainer
{
    public ModContentPack Mod;
    public IEnumerable<ActiveTextureReport> Reports;
}