using HarmonyLib;
using RimWorld;
using System;
using System.Linq;
using System.Text;
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
            Log("Hello, world!");
            var h = new Harmony("co.uk.epicguru.animations");
            h.PatchAll();
            ModContent = content;

            LongEventHandler.ExecuteWhenFinished(() =>
            {
                AnimRenderer.DefaultCutout ??= new Material(ThingDefOf.AIPersonaCore.graphic.Shader);
                AnimRenderer.DefaultTransparent ??= new Material(FleckDefOf.AirPuff.GetGraphicData(0).shaderType.Shader);
                Log("Assigned default shaders.");

                Log("Loaded misc textures.");
                AnimationManager.Init();

                AnimDef.Init();
                Log("Initialized anim defs.");

                LogPotentialConflicts(h);
            });
        }       
    
        private void LogPotentialConflicts(Harmony h)
        {
            bool IsSelf(Patch p)
            {
                return p != null && p.owner == h.Id;
            }

            var str = new StringBuilder();
            int conflicts = 0;
            foreach(var changed in h.GetPatchedMethods())
            {
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
            }

            if (conflicts > 0)
            {
                Warn($"Potential patch conflicts ({conflicts}):");
                Warn(str.ToString());
            }
            else
            {
                Log("No Harmony patch conflicts were detected.");
            }
        }
    }

    public class HotSwappableAttribute : Attribute
    {

    }

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

            if (Input.GetKeyDown(KeyCode.P))
                StartAnim();

            if (!Input.GetKeyDown(KeyCode.E))
                return;

            var mainPawn = Find.CurrentMap?.mapPawns?.AllPawns.Where(p => !p.Dead && p.IsColonist).FirstOrDefault();
            Pawn otherPawn = null;
            int tries = 0;
            while (otherPawn == null || otherPawn == mainPawn)
            {
                otherPawn = Find.CurrentMap?.mapPawns?.AllPawns.Where(p => !p.Dead && p.IsColonist && !p.RaceProps.Animal).RandomElement();
                tries++;
                if (tries > 100)
                    return;
            }

            var rootTransform = mainPawn.MakeAnimationMatrix();

            var manager = map.GetAnimManager();

            // Cancel any previous animation(s)
            manager.StopAnimation(mainPawn);
            manager.StopAnimation(otherPawn);

            var def = DefDatabase<AnimDef>.GetNamed("AAM_Duel_Swords");

            var anim = manager.StartAnimation(def, rootTransform, mainPawn, otherPawn);
            anim.MirrorHorizontal = Rand.Bool;
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
