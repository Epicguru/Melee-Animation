using AAM.Retexture;
using AAM.Tweaks;
using HarmonyLib;
using ModRequestAPI;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace AAM
{
    [HotSwapAll]
    public class Core : Mod
    {
        private static readonly string GistID = "d1c22be7a26feb273008c4cea948be53";

        public static Func<Pawn, float> GetBodyDrawSizeFactor = _ => 1f;
        public static string ModTitle => ModContent?.Name;
        public static ModContentPack ModContent;
        public static Settings Settings;
        public static bool IsSimpleSidearmsActive;

        private readonly Queue<(string title, Action action)> lateLoadActions = new();
        private readonly Queue<(string title, Action action)> lateLoadActionsSync = new();

        public static void Log(string msg)
        {
            Verse.Log.Message($"<color=green>[ThingGenerator]</color> {msg}");
        }

        public static void Warn(string msg)
        {
            Verse.Log.Warning($"<color=green>[ThingGenerator]</color> {msg}");
        }

        public static void Error(string msg, Exception e = null)
        {
            Verse.Log.Error($"<color=green>[ThingGenerator]</color> {msg}");
            if (e != null)
                Verse.Log.Error(e.ToString());
        }

        [DebugAction("Advanced Melee Animation", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Entry)]
        private static void LogModRequests()
        {
            var task = Task.Run(() => new ModRequestClient(GistID).GetModRequests());
            task.ContinueWith(t =>
            {
                if (!t.IsCompletedSuccessfully)
                {
                    Error("Failed to get mod requests:", t.Exception);
                    return;
                }

                var list = from r in t.Result orderby r.data.RequestCount descending select r;
                foreach (var pair in list)
                {
                    Log($"[{pair.modID}] {pair.data.RequestCount} requests with {pair.data.MissingWeaponCount} missing weapons.");
                }
            });
        }

        public Core(ModContentPack content) : base(content)
        {
            AddParsers();

            string assemblies = string.Join(",\n", from a in content.assemblies.loadedAssemblies select a.FullName);
            Log($"Hello, world!\n\nLoaded assemblies ({content.assemblies.loadedAssemblies.Count}):\n{assemblies}");

            var h = new Harmony(content.PackageId);
            h.PatchAll();
            ModContent = content; 

            // Initialize settings.
            Settings = GetSettings<Settings>();

            AddLateLoadAction(true, "Loading default shaders", () =>
            {
                AnimRenderer.DefaultCutout ??= new Material(ThingDefOf.AIPersonaCore.graphic.Shader);
                AnimRenderer.DefaultTransparent ??= new Material(ShaderTypeDefOf.Transparent.Shader);
                //AnimRenderer.DefaultTransparent ??= new Material(DefDatabase<ShaderTypeDef>.GetNamed("Mote").Shader);
            });

            AddLateLoadAction(false, "Checking for Simple Sidearms install...", CheckSimpleSidearms);
            AddLateLoadAction(false, "Checking for patch conflicts...", () => LogPotentialConflicts(h));
            AddLateLoadAction(false, "Finding all lassos...", AAM.Content.FindAllLassos);

            AddLateLoadAction(true, "Loading main content...", AAM.Content.Load);
            AddLateLoadAction(true, "Loading misc textures...", AnimationManager.Init);
            AddLateLoadAction(true, "Initializing anim defs...", AnimDef.Init);
            AddLateLoadAction(true, "Applying settings...", Settings.PostLoadDefs);
            AddLateLoadAction(true, "Matching textures with mods...", PreCacheAllRetextures);
            AddLateLoadAction(true, "Loading weapon tweak data...", LoadAllTweakData);

            AddLateLoadEvents();
        }

        private static void PreCacheAllRetextures()
        {
            var time = RetextureUtility.PreCacheAllTextureReports(rep =>
            {
                if (rep.HasError)
                {
                    Error($"Error generating texture report [{rep.Weapon?.LabelCap}]: {rep.ErrorMessage}");
                }
            }, false);
            Log($"PreCached all retexture info in {time.TotalMilliseconds:F1}ms");
        }

        private void LoadAllTweakData()
        {
            var modsAndMissingWeaponCount = new Dictionary<string, int>();

            foreach (var pair in TweakDataManager.LoadAllForActiveMods(false))
            {
                if (!modsAndMissingWeaponCount.ContainsKey(pair.modPackageID))
                    modsAndMissingWeaponCount.Add(pair.modPackageID, 0);

                modsAndMissingWeaponCount[pair.modPackageID]++;
            }

            Log($"Loaded tweak data for {TweakDataManager.TweakDataLoadedCount} weapons.");

            if (modsAndMissingWeaponCount.Count == 0)
                return;

            foreach (var pair in modsAndMissingWeaponCount)
            {
                Warn($"{pair.Key} has {pair.Value} missing weapon tweak data.");
            }

            Task.Run(() => UploadMissingModData(modsAndMissingWeaponCount)).ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                {
                    Log("Successfully reported missing mod/weapons.");
                    return;
                }

                Error("Reporting missing mod/weapons failed with exception:", t.Exception);
            });
        }

        private static async Task UploadMissingModData(Dictionary<string, int> modAndWeaponCounts)
        {
            var client = new ModRequestClient(GistID);

            bool UpdateAction(string modID, ModData data)
            {
                int missingWeaponCount = modAndWeaponCounts[modID];

                data.RequestCount++;
                if (data.MissingWeaponCount < missingWeaponCount)
                    data.MissingWeaponCount = missingWeaponCount;

                return true;
            }

            string desc = $"APIUpdate at {DateTime.UtcNow}. Game Version: {VersionControl.CurrentVersionStringWithRev}. Using steam: {SteamUtility.SteamPersonaName != null && SteamUtility.SteamPersonaName != "???"}";
            await client.UpdateModRequests(modAndWeaponCounts.Keys, desc, UpdateAction);
        }

        private void AddLateLoadEvents()
        {
            // Different thread loading...
            LongEventHandler.QueueLongEvent(() =>
            {
                LongEventHandler.SetCurrentEventText("Load Advanced Animation Mod");
                while (lateLoadActions.TryDequeue(out var pair))
                {
                    try
                    {
                        LongEventHandler.SetCurrentEventText($"{ModTitle}: {pair.title}\n");
                        pair.action();
                    }
                    catch (Exception e)
                    {
                        Error($"Exception in post-load event (async) '{pair.title}':", e);
                    }
                }
            }, "Load Advanced Animation Mod", true, null);

            // Same thread loading...
            LongEventHandler.QueueLongEvent(() =>
            {
                while (lateLoadActionsSync.TryDequeue(out var pair))
                {
                    try
                    {
                        LongEventHandler.SetCurrentEventText($"{ModTitle}:\n{pair.title}");
                        pair.action();
                    }
                    catch (Exception e)
                    {
                        Error($"Exception in post-load event '{pair.title}':", e);
                    }
                }
            }, "Load Advanced Animation Mod", false, null);
        }

        private void AddLateLoadAction(bool synchronous, string title, Action a)
        {
            if (a == null)
                return;
            (synchronous ? lateLoadActionsSync : lateLoadActions).Enqueue((title, a));
        }

        private void AddParsers()
        {
            AddParser(byte.Parse);
            AddParser(decimal.Parse);
            AddParser(short.Parse);
            AddParser(ushort.Parse);
            AddParser(uint.Parse);
            AddParser(ulong.Parse);
        }

        private void AddParser<T>(Func<string, T> func)
        {
            if (func == null)
                return;

            // We need to do two checks because of a Rimworld bug in the HandlesType method.
            // If the T is a primitive type, HandlesType returns true, even though it is not actually handled.
            if (typeof(T).IsPrimitive && ParseHelper.CanParse(typeof(T), default(T).ToString()))
            {
                Warn($"There is already a parser for the type '{typeof(T).FullName}'. I wonder who added it...");
                return;
            }
            if (!typeof(T).IsPrimitive && ParseHelper.HandlesType(typeof(T)))
            {
                Warn($"There is already a parser for the type '{typeof(T).FullName}'. I wonder who added it...");
                return;
            }

            ParseHelper.Parsers<T>.Register(func);
        }

        public override string SettingsCategory() => ModTitle;

        public override void DoSettingsWindowContents(Rect inRect)
        {
            SimpleSettings.DrawWindow(Settings, inRect);
        }

        private void LogPotentialConflicts(Harmony h)
        {
            bool IsSelf(Patch p)
            {
                return p != null && p.owner == h.Id;
            }

            var str = new StringBuilder();
            var str2 = new StringBuilder();
            var str3 = new StringBuilder();
            int conflicts = 0;
            foreach(var changed in h.GetPatchedMethods())
            {
                int oldConflicts = conflicts;
                var patches = Harmony.GetPatchInfo(changed);
                str.AppendLine();
                str.AppendLine(changed.FullDescription());

                str.AppendLine("Prefixes:");
                foreach(var pre in patches.Prefixes)
                {
                    str.AppendLine($"  [{pre.owner}] {pre.PatchMethod.Name}");
                    if (!IsSelf(pre))
                        conflicts++;
                }

                str.AppendLine("Transpilers:");
                foreach (var trans in patches.Transpilers)
                {
                    str.AppendLine($"  [{trans.owner}] {trans.PatchMethod.Name}");
                    if (!IsSelf(trans))
                        conflicts++;
                }

                str.AppendLine("Postfixes:");
                foreach (var post in patches.Postfixes)
                {
                    str.AppendLine($"  [{post.owner}] {post.PatchMethod.Name}");
                    if (!IsSelf(post))
                        conflicts++;
                }

                str2.Append(str);
                if (oldConflicts != conflicts)
                    str3.Append(str);
                str.Clear();
            }

            if (conflicts > 0)
            {
                Warn($"Potential patch conflicts ({conflicts}):");
                Warn(str3.ToString());
            }
            else
            {
                Log("No Harmony patch conflicts were detected.");
            }

            Log("Full patch list:");
            Log(str2.ToString());
        }

        private void CheckSimpleSidearms()
        {
            IsSimpleSidearmsActive = ModLister.GetActiveModWithIdentifier("PeteTimesSix.SimpleSidearms") != null;
        }
    }

    public class HotSwapAllAttribute : Attribute { }
}
