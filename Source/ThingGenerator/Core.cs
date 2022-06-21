using AAM.Grappling;
using AAM.Tweaks;
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AAM
{
    [HotSwapAll]
    public class Core : Mod
    {
        public static string ModFolder => ModContent.RootDir;
        public static ModContentPack ModContent;
        public static Settings Settings;
        public static bool IsSimpleSidearmsActive;

        private readonly Queue<(string title, Action action)> lateLoadActions = new Queue<(string, Action)>();
        private readonly Queue<(string title, Action action)> lateLoadActionsSync = new Queue<(string, Action)>();

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

        public Core(ModContentPack content) : base(content)
        {
            AddParsers();

            Log("Hello, world!");
            var h = new Harmony("co.uk.epicguru.animations");
            h.PatchAll();
            ModContent = content;

            Settings = GetSettings<Settings>();

            AddLateLoadAction(true, "Loading default shaders", () =>
            {
                AnimRenderer.DefaultCutout ??= new Material(ThingDefOf.AIPersonaCore.graphic.Shader);
                AnimRenderer.DefaultTransparent ??= new Material(FleckDefOf.AirPuff.GetGraphicData(0).shaderType.Shader);
            });

            AddLateLoadAction(true, "Loading misc textures...", AnimationManager.Init);
            AddLateLoadAction(true, "Loading line renderer...", AAM.Content.Load);
            AddLateLoadAction(false, "Initializing anim defs...", AnimDef.Init);
            AddLateLoadAction(false, "Checking for Simple Sidearms install...", CheckSimpleSidearms);
            AddLateLoadAction(true, "Checking for patch conflicts...", () => LogPotentialConflicts(h));
            AddLateLoadAction(false, "Loading weapon tweak data...", () => TweakDataManager.LoadAllForActiveMods());
            AddLateLoadAction(true, "Finding all lassos...", AAM.Content.FindAllLassos);

            AddLateLoadEvents();
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
                        LongEventHandler.SetCurrentEventText($"Advanced Animation: {pair.title}\n");
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
                        LongEventHandler.SetCurrentEventText($"Advanced Animation:\n{pair.title}");
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

        public override string SettingsCategory()
        {
            return Content.Name;
        }

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

    public class TempComp : MapComponent
    {
        public TempComp(Map map) : base(map)
        {
        }

        public override void MapComponentUpdate()
        {
            base.MapComponentTick();

            var sel = Find.Selector.SelectedPawns.Where(p => p.Spawned && !p.Dead && !p.Downed).ToList();
            if (sel.Count == 1)
            {
                var selectedPawn = sel[0];
                if (selectedPawn != null && Input.GetKey(KeyCode.LeftControl))
                    DrawValidSpotsAround(selectedPawn);

                var targets = selectedPawn.Map.attackTargetsCache.GetPotentialTargetsFor(selectedPawn);
                foreach (var target in targets)
                {
                    if (target.Thing is not Pawn pawn)
                        continue;

                    SimpleColor color = SimpleColor.Green;
                    if (target.ThreatDisabled(selectedPawn))
                        continue;
                    if (!AttackTargetFinder.IsAutoTargetable(target))
                        continue;

                    GenDraw.DrawLineBetween(selectedPawn.DrawPos, target.Thing.DrawPos, color);
                }
            }
        }

        private void DrawValidSpotsAround(Pawn pawn)
        {
            foreach (var cell in GrabUtility.GetFreeSpotsAround(pawn))
                GenDraw.DrawTargetHighlightWithLayer(cell, AltitudeLayer.MoteOverhead);
        }
    }
}
