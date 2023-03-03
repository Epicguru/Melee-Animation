using AAM.Grappling;
using RimWorld;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AAM.Processing;

public class MapPawnProcessor : IDisposable
{
    [TweakValue("Advanced Melee Animation", 0f, 5f)]
    public static float PawnPerThreadThreshold = 0.2f;
    [TweakValue("Advanced Melee Animation")]
    public static bool LogPerformanceToDesktop;

    private static readonly Dictionary<int, Task[]> taskArrayPool = new Dictionary<int, Task[]>();
    private static StreamWriter debugWriter;

    private static Task[] GetTaskArray(int count)
    {
        if (taskArrayPool.TryGetValue(count, out var found))
        {
            return found;
        }

        found = new Task[count];
        taskArrayPool.Add(count, found);
        return found;
    }

    public readonly DiagnosticInfo Diagnostics = new DiagnosticInfo();

    private readonly List<AttackerData> attackers = new List<AttackerData>(128);
    private readonly List<IntRange> slices = new List<IntRange>(32);
    private readonly List<TaskData> targetsPool = new List<TaskData>();
    private readonly ConcurrentQueue<(AnimationStartParameters args, IntVec3? lassoToHere)> toStart = new ConcurrentQueue<(AnimationStartParameters, IntVec3?)>();
    private readonly ActionController generalController = new ActionController();
    private readonly Map map;
    private int targetsIndex;

    public MapPawnProcessor(Map map)
    {
        this.map = map;
    }

    private TaskData GetTaskData()
    {
        if (targetsIndex == targetsPool.Count)
        {
            var created = new TaskData(targetsIndex);
            targetsIndex++;
            targetsPool.Add(created);
            return created;
        }

        var found = targetsPool[targetsIndex];
        targetsIndex++;
        return found;
    }

    public void Tick()
    {
        if (GenTicks.TicksAbs % Core.Settings.ScanTickInterval != 0)
            return;

        if (LogPerformanceToDesktop && debugWriter == null)
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "AnimationModPerformanceReport.csv");
            debugWriter = new StreamWriter(path, true);
        }
        else
        {
            Dispose();
        }

        var timer = new RefTimer();

        toStart.Clear();

        // Get all pawns that need auto processing.
        CompileListOfAttackers();

        // Make slices.
        slices.Clear();
        slices.AddRange(MakeProcessingSlices(attackers.Count));

        // Make tasks.
        var timer2 = new RefTimer();
        targetsIndex = 0;
        if (slices.Count == 1)
        {
            // Run sync.
            var taskData = GetTaskData();
            taskData.ScheduleTick = RefTimer.GetTickNow();
            ProcessSlice(attackers, new IntRange(0, attackers.Count - 1), map, taskData, toStart, Diagnostics);
        }
        else if (slices.Count > 1)
        {
            // Run async.
            var arr = GetTaskArray(slices.Count);

            // Generate tasks.
            for (int i = 0; i < slices.Count; i++)
            {
                int j = i;
                var taskData = GetTaskData();
                taskData.ScheduleTick = RefTimer.GetTickNow();
                var task = Task.Run(() => ProcessSlice(attackers, slices[j], map, taskData, toStart, Diagnostics));
                arr[i] = task;
            }

            // Wait for them to complete.
            Task.WaitAll(arr);

            // Clean array.
            for (int i = 0; i < arr.Length; i++)
                arr[i] = null;
        }

        // Debug output, optional.
        debugWriter?.WriteLine($"{attackers.Count},{slices.Count},{timer2.GetElapsedMilliseconds().ToString(CultureInfo.InvariantCulture)},{PawnPerThreadThreshold}");

        // Start all pending animations.
        // Must be done here, in main thread, because otherwise
        // sweep meshes created will crash unity.
        while (toStart.TryDequeue(out var pair))
        {
            var args = pair.args;
            if (pair.lassoToHere == null)
            {
                // Instant trigger.
                var finalArgs = args with
                {
                    ExecutionOutcome = OutcomeUtility.GenerateRandomOutcome(args.MainPawn, args.SecondPawn),
                };
                bool worked = finalArgs.TryTrigger();

                // Set execution cooldown.
                if (worked)
                    args.MainPawn.GetMeleeData().TimeSinceExecuted = 0;
            }
            else
            {
                var finalArgs = args with
                {
                    ExecutionOutcome = OutcomeUtility.GenerateRandomOutcome(args.MainPawn, args.SecondPawn),
                };

                // Lasso.
                if (!JobDriver_GrapplePawn.GiveJob(args.MainPawn, args.SecondPawn, pair.lassoToHere.Value, false, finalArgs))
                {
                    Core.Error($"Failed to give grapple job to {args.MainPawn}.");
                    return;
                }

                // Set lasso cooldown. Execution cooldown is set buy the job driver.
                args.MainPawn.GetMeleeData().TimeSinceGrappled = 0;
            }
            
        }

        Diagnostics.ThreadsUsed = slices.Count;
        timer2.GetElapsedMilliseconds(out Diagnostics.ProcessTimeMS);
        timer.GetElapsedMilliseconds(out Diagnostics.TotalTimeMS);
    }

    public void Dispose()
    {
        if (debugWriter == null)
            return;

        debugWriter.Dispose();
        debugWriter = null;
    }

    private static void GetPotentialTargets(List<IAttackTarget> output, Pawn attacker, AttackTargetsCache cache, Map map)
    {
        Thing thing = attacker;
        output.Clear();
        Faction faction = thing.Faction;
        if (faction != null)
        {
            foreach (IAttackTarget attackTarget in cache.TargetsHostileToFaction(faction))
            {
                if (thing.HostileTo(attackTarget.Thing))
                {
                    output.Add(attackTarget);
                }
            }
        }
        foreach (Pawn pawn in cache.pawnsInAggroMentalState)
        {
            if (thing.HostileTo(pawn))
            {
                output.Add(pawn);
            }
        }
        foreach (Pawn pawn2 in cache.factionlessHumanlikes)
        {
            if (thing.HostileTo(pawn2))
            {
                output.Add(pawn2);
            }
        }
        if (PrisonBreakUtility.IsPrisonBreaking(attacker))
        {
            Faction hostFaction = attacker.guest.HostFaction;
            List<Pawn> list = map.mapPawns.SpawnedPawnsInFaction(hostFaction);
            foreach (Pawn pawn in list)
            {
                if (thing.HostileTo(pawn))
                {
                    output.Add(pawn);
                }
            }
        }
        if (ModsConfig.IdeologyActive && SlaveRebellionUtility.IsRebelling(attacker))
        {
            Faction faction2 = attacker.Faction;
            List<Pawn> list2 = map.mapPawns.SpawnedPawnsInFaction(faction2);
            for (int j = 0; j < list2.Count; j++)
            {
                if (thing.HostileTo(list2[j]))
                {
                    output.Add(list2[j]);
                }
            }
        }
    }

    private static bool TargetFilter(IAttackTarget target)
    {
        if (target.Thing is not Pawn targetPawn)
            return false;

        // Target cannot be dead or downed.
        if (targetPawn.Dead || targetPawn.Downed || targetPawn.IsInAnimation())
            return false;

        return true;
    }

    private static void ProcessSlice(List<AttackerData> attackers, IntRange slice, Map map, TaskData taskData, ConcurrentQueue<(AnimationStartParameters, IntVec3?)> startArgs, DiagnosticInfo diag)
    {
        diag.StartupTimesMS[taskData.Index] = RefTimer.ToMilliseconds(taskData.ScheduleTick, RefTimer.GetTickNow());
        diag.TargetFindTimesMS[taskData.Index] = 0;
        diag.ReportTimesMS[taskData.Index] = 0;

        try
        {
            for (int i = slice.min; i <= slice.max; i++)
            {
                var data = attackers[i];
                var pawn = data.Pawn;

                // Make a list of enemies.
                var targetsTimer = new RefTimer();
                GetPotentialTargets(taskData.Targets, pawn, map.attackTargetsCache, map);
                diag.TargetFindTimesMS[taskData.Index] += targetsTimer.GetElapsedMilliseconds();

                if (taskData.Targets.Count == 0)
                    continue;

                // Make space mask around attacker.
                ulong occupiedMask = SpaceChecker.MakeOccupiedMask(map, pawn.Position, out uint smallMask);
                bool westFree = !occupiedMask.GetBit(-1, 0);
                bool eastFree = !occupiedMask.GetBit(1, 0);

                // Do instant executions:
                PotentialAnimation toPerform = default;
                var reportTimer = new RefTimer();
                if (data.CanExecute && (eastFree || westFree))
                {
                    var reports = taskData.Controller.GetExecutionReport(new ExecutionAttemptRequest
                    {
                        CanUseLasso = data.CanGrapple,
                        CanWalk = false,
                        EastCell = eastFree,
                        WestCell = westFree,
                        Executioner = pawn,
                        NoErrorMessages = true,
                        OccupiedMask = occupiedMask,
                        SmallOccupiedMask = smallMask,
                        TrustLassoUsability = true,
                        LassoRange = data.LassoRange,
                        Targets = taskData.Targets.Where(TargetFilter).Select(t => (Pawn)t)
                    });

                    foreach (var report in reports)
                    {
                        if (!report.CanExecute)
                            continue;

                        if (report.IsFinal)
                            throw new Exception("Should not be getting final report here!");

                        if (report.IsWalking)
                            throw new Exception("Walking was disallowed but still showed up!");

                        var selected = report.PossibleExecutions.RandomElementByWeightWithFallback(p => p.Animation.AnimDef.Probability);
                        if (!selected.IsValid)
                        {
                            Core.Warn("Failed to get any valid animation even through they were presented in the report.");
                            continue;
                        }

                        // Populate the action to be performed:
                        toPerform = new PotentialAnimation
                        {
                            Anim = selected.Animation.AnimDef,
                            FlipX = selected.Animation.FlipX,
                            Target = report.Target,
                            LassoToHere = selected.LassoToHere
                        };
                        break;
                    }

                    foreach (var report in reports)
                        report.Dispose();
                }
                diag.ReportTimesMS[taskData.Index] += reportTimer.GetElapsedMilliseconds();

                if (toPerform.IsValid)
                {
                    // Start instant execution animation.
                    startArgs.Enqueue((new AnimationStartParameters(toPerform.Anim, pawn, toPerform.Target)
                    {
                        FlipX = toPerform.FlipX
                    }, toPerform.LassoToHere));
                }
            }
        }
        catch (Exception e)
        {
            Core.Error("Processing thread error:", e);
        }
    }

    private static void GetSecondsMTB(Pawn pawn, out float execute, out float lasso)
    {
        if (pawn.IsColonist || pawn.IsSlaveOfColony)
        {
            execute = Core.Settings.ExecuteAttemptMTBSeconds;
            lasso = 10;
        }
        else
        {
            execute = Core.Settings.ExecuteAttemptMTBSecondsEnemy;
            lasso = 10; // TODO implement lasso MTB
        }
    }

    /// <summary>
    /// Puts together a list of pawns that have either a melee weapon or a lasso, or both,
    /// and have auto-lasso and/or execute enabled.
    /// </summary>
    private void CompileListOfAttackers()
    {
        var timer = new RefTimer();

        bool FormalGrappleCheck(Pawn pawn)
        {
            return generalController.GetGrappleReport(new GrappleAttemptRequest
            {
                Grappler = pawn,
                DoNotCheckCooldown = true,
                DoNotCheckLasso = true,
                GrappleSpotPickingBehaviour = GrappleSpotPickingBehaviour.Closest,
                NoErrorMessages = true
            }).CanGrapple;
        }

        attackers.Clear();
        foreach (var pawn in map.mapPawns.AllPawnsSpawned)
        {
            // Not dead or downed.
            if (pawn.Dead || pawn.Downed)
                continue;

            // Not animal, can ever use tools.
            if (pawn.RaceProps.Animal || !pawn.def.race.ToolUser)
                continue;

            // Not already in animation or being grappled.
            if (pawn.IsInAnimation() || GrabUtility.IsBeingTargetedForGrapple(pawn))
                continue;

            // Should this pawn even be scanned?
            GetSecondsMTB(pawn, out float execMTB, out float lassoMTB);
            bool execRandom = Rand.MTBEventOccurs(execMTB, 60f, Core.Settings.ScanTickInterval);
            bool lassoRandom = Rand.MTBEventOccurs(lassoMTB, 60f, Core.Settings.ScanTickInterval);

            if (!execRandom && !lassoRandom)
                continue;

            var mData  = pawn.GetMeleeData();
            var weapon = pawn.GetFirstMeleeWeapon();
            var lasso  = pawn.TryGetLasso();

            // Melee skill has to be extracted here, on the main thread,
            // because concurrent access throws exceptions.
            var data = new AttackerData
            {
                Pawn = pawn,
                PawnMeleeLevel = pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0,
                MeleeWeapon = weapon,
                Lasso = lasso,
                CanExecute = execRandom && weapon != null && mData.ResolvedAutoExecute && mData.IsExecutionOffCooldown(),
                CanGrapple = lassoRandom && lasso != null && mData.ResolvedAutoGrapple && mData.IsGrappleOffCooldown() && FormalGrappleCheck(pawn),
                LassoRange = lasso != null ? pawn.GetStatValue(AAM_DefOf.AAM_GrappleRadius) : 0
            };

            if (!data.CanGrapple && !data.CanExecute)
                continue;

            attackers.Add(data);
        }

        Diagnostics.PawnCount = attackers.Count;
        timer.GetElapsedMilliseconds(out Diagnostics.CompileTimeMS);
    }

    public static int GetWorkerThreadCount() => Mathf.Max(1, JobsUtility.JobWorkerCount + 1);

    public static IEnumerable<IntRange> MakeProcessingSlices(int todo)
    {
        if (todo <= 0)
            yield break;

        // Max threads allowed, from settings:
        int maxThreads = Core.Settings.MaxProcessingThreads <= 0 ? GetWorkerThreadCount() : Core.Settings.MaxProcessingThreads;

        // Number of threads scales up to maximum allowed. Do not allow more than 1 thread per pawn.
        int threads = Mathf.Min(todo, maxThreads);

        // Thread per pawn threshold calculation:
        float basicPer = (float)todo / maxThreads;
        if (basicPer < PawnPerThreadThreshold)
            threads = 1;

        if (threads == 1)
        {
            yield return new IntRange(0, todo - 1);
            yield break;
        }

        // Do slicing:
        int per = todo / threads;
        int extraCount = todo % threads;
        int start = 0;
        for (int i = 0; i < threads; i++)
        {
            int count = per;
            if (i < extraCount)
                count++;

            yield return new IntRange(start, start + count - 1);
            start += count;
        }

        if (start != todo)
            throw new Exception("Failed to slice correctly.");
    }

    #region structs
    private readonly struct AttackerData
    {
        public required Pawn Pawn { get; init; }
        public required int PawnMeleeLevel { get; init; }
        public required Thing MeleeWeapon { get; init; } 
        public required Thing Lasso { get; init; }
        public required bool CanGrapple { get; init; }
        public required bool CanExecute { get; init; }
        public required float LassoRange { get; init; }
    }

    private readonly struct PotentialAnimation
    {
        public readonly bool IsValid => Anim != null;

        public required AnimDef Anim { get; init; }
        public required bool FlipX { get; init; }
        public required Pawn Target { get; init; }
        public IntVec3? LassoToHere { get; init; }
    }

    private class TaskData
    {
        public readonly List<IAttackTarget> Targets = new List<IAttackTarget>(64);
        public readonly List<PotentialAnimation> EastAnimations = new List<PotentialAnimation>();
        public readonly List<PotentialAnimation> WestAnimations = new List<PotentialAnimation>();
        public readonly ActionController Controller = new ActionController();
        public readonly int Index;
        public long ScheduleTick;

        public TaskData(int index)
        {
            Index = index;
        }
    }

    public class DiagnosticInfo
    {
        public readonly double[] StartupTimesMS = new double[64];
        public readonly double[] TargetFindTimesMS = new double[64];
        public readonly double[] ReportTimesMS = new double[64];
        public int ThreadsUsed;
        public double CompileTimeMS;
        public double ProcessTimeMS;
        public double TotalTimeMS;
        public int PawnCount;
    }
    #endregion
}
