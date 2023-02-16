using System.Collections.Generic;
using AAM.Retexture;
using System.IO;
using UnityEngine;
using Verse;
using System.Linq;
using System;

namespace AAM.Tweaks
{
    public static class TweakDataManager
    {
        public const string DATA_FOLDER_NAME = "WeaponTweakData";

        public static int TweakDataLoadedCount => defWithModToTweak.Count;

        private static readonly Dictionary<ThingDef, ItemTweakData> defToTweak = new Dictionary<ThingDef, ItemTweakData>();
        private static readonly Dictionary<(ThingDef def, ModContentPack mod), ItemTweakData> defWithModToTweak = new Dictionary<(ThingDef def, ModContentPack mod), ItemTweakData>();

        /// <summary>
        /// Gets the <see cref="FileInfo"/> where the weapon tweak data is expected to exist
        /// </summary>
        /// <param name="weaponDef"></param>
        /// <param name="textureMod"></param>
        /// <returns></returns>
        public static FileInfo GetFileForTweak(ThingDef weaponDef, ModContentPack textureMod = null)
        {
            if (textureMod == null)
            {
                var report = RetextureUtility.GetTextureReport(weaponDef);
                textureMod = report.ActiveRetextureMod;
            }

            string fileName = ItemTweakData.MakeFileName(weaponDef, textureMod);
            string root = Core.ModContent.RootDir;
            string fp = Path.Combine(root, DATA_FOLDER_NAME, fileName);
            return new FileInfo(fp);
        }

        public static ItemTweakData TryGetTweak(ThingDef def) => TryGetTweak(def, null);

        public static ItemTweakData TryGetTweak(ThingDef def, ModContentPack textureSupplier)
        {
            if (textureSupplier == null)
                return defToTweak.TryGetValue(def, out var found) ? found : null;
            
            return defWithModToTweak.TryGetValue((def, textureSupplier), out var found2) ? found2 : null;
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
                return null;

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

        [DebugOutput("Advanced Melee Animation")]
        private static void LogAllRetextureCompletion()
        {
            var all = from td in DefDatabase<ThingDef>.AllDefsListForReading
                      where td.IsMeleeWeapon
                      let report = RetextureUtility.GetTextureReport(td)
                      where !report.HasError
                      from pair in report.AllRetextures
                      select ( pair.mod, report );

            static TableDataGetter<(ModContentPack mod, ActiveTextureReport rep)> Row(string name, Func<(ModContentPack mod, ActiveTextureReport rep), string> toString) 
                => new TableDataGetter<(ModContentPack mod, ActiveTextureReport rep)>(name, toString);

            static TableDataGetter<(ModContentPack mod, ActiveTextureReport rep)> RowThing(string name, Func<(ModContentPack mod, ActiveTextureReport rep), ThingDef> toString)
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

        public static IEnumerable<(string modPackageID, ThingDef weapon)> LoadAllForActiveMods(bool includeRedundant)
        {
            var data = from weapon in DefDatabase<ThingDef>.AllDefsListForReading
                where weapon.IsMeleeWeapon
                let report = RetextureUtility.GetTextureReport(weapon)
                select (weapon, report);

            foreach (var pair in data)
            {
                if (pair.report.HasError)
                {
                    Core.Error($"Failed to get texture report for {pair.weapon}: {pair.report.ErrorMessage}");
                    continue;
                }

                var tweak = TryLoad(pair.weapon, pair.report.ActiveRetextureMod);
                if (tweak == null)
                    yield return (ItemTweakData.MakeModID(pair.report.ActiveRetextureMod), pair.weapon);

                if (includeRedundant)
                {
                    foreach (var retex in pair.report.AllRetextures)
                    {
                        if (retex.mod == pair.report.ActiveRetextureMod)
                            continue;

                        tweak = TryLoad(pair.weapon, retex.mod);
                        if (tweak == null)
                            yield return (ItemTweakData.MakeModID(pair.report.ActiveRetextureMod), pair.weapon);
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
}
