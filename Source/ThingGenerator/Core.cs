using AAM.Grappling;
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using AAM.Tweaks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AAM
{
    [HotSwappable]
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
            AddLateLoadAction(true, "Loading main content...", SweepPathRenderer.Init);
            AddLateLoadAction(false, "Initializing anim defs...", AnimDef.Init);
            AddLateLoadAction(false, "Checking for Simple Sidearms install...", CheckSimpleSidearms);
            AddLateLoadAction(true, "Checking for patch conflicts...", () => LogPotentialConflicts(h));
            AddLateLoadAction(false, "Loading weapon tweak data...", () => TweakDataManager.LoadAllForActiveMods());

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

    public class HotSwappableAttribute : Attribute { }

    public class TempComp : MapComponent
    {
        [DebugAction("Advanced Animation Mod", "Debug Execute", false, false, actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugExecute(Pawn victim)
        {
            var executioner  = Find.CurrentMap?.mapPawns?.AllPawns.Where(p => !p.Dead && p.IsColonist).FirstOrDefault();
            StartExecution(executioner, victim);
        }

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
            }
            else if (sel.Count >= 2)
            {
                var main = sel[0];
                var map = main.Map;

                IEnumerable<Pawn> Others()
                {
                    for (int i = 1; i < sel.Count; i++)
                    {
                        yield return sel[i];
                    }
                }

                var canDo = new List<GrabUtility.PossibleExecution>();
                foreach (var exec in GrabUtility.GetPossibleExecutions(main, Others()))
                {
                    if (exec.VictimMoveCell != null)
                    {
                        bool hasLOS = GenSight.LineOfSightToThing(exec.VictimMoveCell.Value, exec.Victim, map);

                        if(Input.GetKey(KeyCode.LeftControl))
                            GenDraw.DrawLineBetween(exec.Victim.DrawPos, exec.VictimMoveCell.Value.ToVector3Shifted(), !hasLOS ? SimpleColor.Red : exec.MirrorX ? SimpleColor.Magenta : SimpleColor.Cyan);

                        if (hasLOS)
                        {
                            canDo.Add(exec);
                        }
                    }

                    Vector2 flat = new Vector2(exec.Victim.DrawPos.x, exec.Victim.DrawPos.z);
                    string text = $"{exec.Def}  (Mirror:{exec.MirrorX})";
                    //GenMapUI.DrawText(flat + new Vector2(1, 0), text, Color.white);
                }

                if (Input.GetKeyDown(KeyCode.G) && canDo.Count > 0)
                {
                    var exec = canDo.RandomElement();
                    var afterGrapple = new AnimationStartParameters(exec.Def, main, exec.Victim)
                    {
                        FlipX = exec.MirrorX,
                        FlipY = exec.MirrorY
                    };
                    if (exec.VictimMoveCell != null)
                        JobDriver_GrapplePawn.GiveJob(main, exec.Victim, exec.VictimMoveCell.Value, false, afterGrapple);
                    else
                        afterGrapple.TryTrigger();
                }
            }

            //if (Input.GetKeyDown(KeyCode.P))
            //    StartAnim();

            if (!Input.GetKeyDown(KeyCode.E))
                return;

            var mainPawn = Find.Selector.SelectedPawns[0];
            var otherPawn = Find.Selector.SelectedPawns[1];

            var rootTransform = mainPawn.MakeAnimationMatrix();

            var manager = map.GetAnimManager();

            // Cancel any previous animation(s)
            manager.StopAnimation(mainPawn);
            manager.StopAnimation(otherPawn);

            var def = DefDatabase<AnimDef>.GetNamed("AAM_Duel_Swords");
            Core.Log($"Starting {mainPawn}, {otherPawn}");
            var anim = manager.StartAnimation(def, rootTransform, mainPawn, otherPawn);
            anim.MirrorHorizontal = false;
        }

        private BezierCurve curve;

        private void DrawBezier()
        {
            int resolution = (int)Mathf.Clamp(10f * Vector2.Distance(curve.P0, curve.P3), 40, 256);
            float y = AltitudeLayer.MoteOverhead.AltitudeFor();

            Vector2 lastPoint = default;
            for (int i = 0; i < resolution; i++)
            {
                float t = i / (resolution - 1f);
                Vector2 point = Bezier.Evaluate(t, curve.P0, curve.P1, curve.P2, curve.P3);

                if (i != 0)
                {
                    Vector3 a = lastPoint.ToWorld(y);
                    Vector3 b = point.ToWorld(y);
                    GenDraw.DrawLineBetween(a, b, SimpleColor.Blue, 0.3f);
                }

                lastPoint = point;
            }

            if (Input.GetKey(KeyCode.Alpha5))
                EditPoint(ref curve.P0);
            //if (Input.GetKey(KeyCode.Alpha2))
            //    EditPoint(ref curve.P1);
            //if (Input.GetKey(KeyCode.Alpha3))
            //    EditPoint(ref curve.P2);
            if (Input.GetKey(KeyCode.Alpha6))
                EditPoint(ref curve.P3);

            Vector2 delta = curve.P3 - curve.P0;
            Vector2 perp = Vector2.Perpendicular(delta.normalized);
            if (curve.P3.x < curve.P0.x)
                perp = -perp;
            float dst = delta.magnitude;

            float t0 = 0.1f;
            float t1 = 0.12f;

            curve.P1 = Vector2.Lerp(curve.P0, curve.P3, t0) + perp * Mathf.Sin(dst * 0.5f) * Mathf.Clamp(dst * 0.25f, 2f, 12f);
            curve.P2 = Vector2.Lerp(curve.P0, curve.P3, t1) + perp * Mathf.Sin(dst * 0.5f) * -Mathf.Clamp(dst * 0.25f, 1f, 6f);

            GenDraw.DrawLineBetween(curve.P0.ToWorld(y), curve.P3.ToWorld(), SimpleColor.Orange, 0.1f);
            GenDraw.DrawLineBetween((curve.P0 + delta.normalized * 2f).ToWorld(y), (curve.P0 + delta.normalized * 2f + perp * 2f).ToWorld(y), SimpleColor.Green, 0.15f);
        }

        private void EditPoint(ref Vector2 point)
        {
            var mp = Verse.UI.MouseMapPosition();
            var mpFlat = new Vector2(mp.x, mp.z);
            point = mpFlat;

            GenDraw.DrawTargetHighlightWithLayer(mp, AltitudeLayer.VisEffects);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref curve, nameof(curve));
        }

        private void StartAnim()
        {
            // Find pawns to do animation on.
            var mainPawn  = Find.CurrentMap?.mapPawns?.AllPawns.Where(p => !p.Dead && p.IsColonist).FirstOrDefault();
            var otherPawn = Find.CurrentMap?.mapPawns?.AllPawns.Where(p => !p.Dead && !p.IsColonist && !p.RaceProps.Animal).RandomElement();

            // Make transform centered around the main pawn's position.
            var rootTransform = mainPawn.MakeAnimationMatrix();

            // Get the current map's animation manager.
            var manager = map.GetAnimManager();

            // Cancel any previous animation(s)
            manager.StopAnimation(mainPawn);
            manager.StopAnimation(otherPawn);

            // Try to find an execution animation to play.
            var exec = AnimDef.TryGetExecutionFor(mainPawn, otherPawn);

            if (exec == null)
            {
                Core.Warn($"Could not find any execution animation to play!");
                return;
            }

            // Start this new animation.
            var anim = manager.StartAnimation(exec, rootTransform, mainPawn, otherPawn);
            //anim.MirrorHorizontal = Rand.Bool;
        }

        private void DrawValidSpotsAround(Pawn pawn)
        {
            foreach (var cell in GrabUtility.GetFreeSpotsAround(pawn))
                GenDraw.DrawTargetHighlightWithLayer(cell, AltitudeLayer.MoteOverhead);
        }

        public static void StartExecution(Pawn executioner, Pawn victim)
        {
            if (executioner == null || victim == null)
                return;

            bool right = Rand.Bool;
            IntVec3 targetCell = victim.Position + (right ? IntVec3.East : IntVec3.West);

            var job = JobMaker.MakeJob(AAM_DefOf.AAM_PrepareAnimation, executioner, victim, targetCell);
            job.collideWithPawns = false;
            job.playerForced = true;

            ClearVerbs(executioner);
            ClearVerbs(victim);

            executioner.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true, null);
        }

        private static void ClearVerbs(Pawn pawn)
        {
            if (pawn.verbTracker?.AllVerbs != null)
                foreach (var verb in pawn.verbTracker.AllVerbs)
                    verb.Reset();


            if (pawn.equipment?.AllEquipmentVerbs != null)
                foreach (var verb in pawn.equipment.AllEquipmentVerbs)
                    verb.Reset();
        }
    }
}
