using AM.Controller.Reports;
using AM.Controller.Requests;
using AM.Grappling;
using AM.Reqs;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AM.Controller;

public class ActionController
{
    public static Func<IntVec3, Map, bool> LOSValidator => Core.Settings.MaxFillPctForLasso >= 0.99f ? AlwaysTrue : MaxFillPct;

    private static bool AlwaysTrue(IntVec3 a, Map b) => true;

    private static bool MaxFillPct(IntVec3 cell, Map map)
    {
        var building = cell.GetEdifice(map);
        if (building == null)
            return true;

        if (building.def.fillPercent <= Core.Settings.MaxFillPctForLasso)
            return true;

        return building is Building_Door { Open: true };
    }

    public static void TryGiveDuelThoughts(Pawn winner, Pawn loser, bool isFriendlyDuel)
    {
        if (winner == null || loser == null)
            return;

        var winThought  = isFriendlyDuel ? AM_DefOf.AM_FriendlyDuel_Win  : throw new NotImplementedException();
        var loseThought = isFriendlyDuel ? AM_DefOf.AM_FriendlyDuel_Lose : throw new NotImplementedException();

        winner.needs.mood.thoughts.memories.TryGainMemory(winThought, loser);
        loser.needs.mood.thoughts.memories.TryGainMemory(loseThought, winner);

        // Give skills.
        winner.skills?.Learn(SkillDefOf.Melee, 1000);
        loser.skills?.Learn(SkillDefOf.Melee, 1000);
    }

    private readonly IntVec3[] closestCells = new IntVec3[8];
    private readonly Comparer comparer = new Comparer();
    private readonly List<PossibleExecution.AnimStartData> tempStartDataList = new List<PossibleExecution.AnimStartData>(64);

    /// <summary>
    /// Checks that a grapple can be performed by a pawn.
    /// Checks all factors.
    /// The returned report indicates whether a grapple can be performed,
    /// and if it can't, specifies why.
    /// </summary>
    public GrappleAttemptReport GetGrappleReport(in GrappleAttemptRequest req)
    {
        if (req.Grappler == null)
            return new GrappleAttemptReport(req, "Internal", "Null grappler");

        // Check spawned.
        if (!req.Grappler.Spawned)
            return new GrappleAttemptReport(req, "Internal", "Grappler not spawned");
        if (req.Target is { Spawned: false })
            return new GrappleAttemptReport(req, "Internal", "Target not spawned");

        // Dead or downed.
        if (req.Grappler.Dead)
            return new GrappleAttemptReport(req, "Dead");
        if (req.Target is { Dead: true })
            return new GrappleAttemptReport(req, "DeadTarget");
        if (req.Grappler.Downed)
            return new GrappleAttemptReport(req, "Downed");

        // Check if grappler is in an animation.
        if (req.Grappler.IsInAnimation())
            return new GrappleAttemptReport(req, "SelfInAnimation");

        var data = req.Grappler.GetMeleeData();

        // Check lasso cooldown.
        if (!req.DoNotCheckCooldown && !data.IsGrappleOffCooldown())
            return new GrappleAttemptReport(req, "Cooldown");

        // Check lasso.
        if (!req.DoNotCheckLasso && req.Grappler.TryGetLasso() == null)
            return new GrappleAttemptReport(req, "MissingLasso");

        // Check skill.
        if (Core.Settings.MinMeleeSkillToLasso > 0 && !req.TrustLassoUsability)
        {
            int meleeSkill = req.Grappler.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
            if (meleeSkill < Core.Settings.MinMeleeSkillToLasso)
                return new GrappleAttemptReport(req, "SkillIssue");
        }

        // Check manipulation.
        if (Core.Settings.MinManipulationToLasso > 0 && !req.TrustLassoUsability)
        {
            float manipulation = req.Grappler.health?.capacities?.GetLevel(PawnCapacityDefOf.Manipulation) ?? 0;
            if (manipulation < Core.Settings.MinManipulationToLasso)
                return new GrappleAttemptReport(req, "Manipulation");
        }

        var map = req.Grappler.Map;

        // If there is no specific target, just check if there are any free spots then stop.
        if (req.Target == null)
        {
            // Get free spots around pawn for grapple.
            int sc = req.OccupiedMask == null ?
                UpdateClosestCells(req, map) :
                UpdateClosestCells(req, req.OccupiedMask.Value);
            return sc == 0 ? new GrappleAttemptReport(req, "NoDest") : GrappleAttemptReport.Success(default);
        }

        // Grappler is not target.
        if (req.Target == req.Grappler)
            return new GrappleAttemptReport(req, "Internal", "Grappler cannot be same as target!");

        // Check maps.
        if (map == null || map != req.Target.Map)
            return new GrappleAttemptReport(req, "Internal", "Maps null or mismatched");

        // Check if target is already in destination spot.
        if (req.DestinationCell != null && req.Target.Position == req.DestinationCell.Value)
            return new GrappleAttemptReport(req, "AlreadyAtTarget");

        // Check if they are already a target.
        if (GrabUtility.IsBeingTargetedForGrapple(req.Target))
            return new GrappleAttemptReport(req, "AlreadyTarget");

        // Check if target is in an animation.
        if (req.Target.IsInAnimation())
            return new GrappleAttemptReport(req, "InAnimation");

        // Check range.
        float range = req.LassoRange ?? req.Grappler.GetStatValue(AM_DefOf.AM_GrappleRadius);
        float dst = req.Grappler.Position.DistanceToSquared(req.Target.Position);
        if (dst > range * range)
            return new GrappleAttemptReport(req, "TooFar");

        // Max mass.
        if (Core.Settings.MaxLassoMass > 0)
        {
            float mass = req.Target.GetStatValue(StatDefOf.Mass);
            if (mass > Core.Settings.MaxLassoMass)
                return new GrappleAttemptReport(req, "Mass");
        }

        // Max size.
        if (Core.Settings.MaxLassoBodySize > 0)
        {
            float size = req.Target.BodySize;
            if (size > Core.Settings.MaxLassoBodySize)
                return new GrappleAttemptReport(req, "Size");
        }

        // If a dest cell is specified, check that it is valid.
        if (req.DestinationCell != null)
        {
            if (!CanStandHere(map, req.DestinationCell.Value))
                return new GrappleAttemptReport(req, "Internal", $"Specified target cell {req.DestinationCell.Value} invalid");

            // Check fixed destination LOS.
            if (!GenSight.LineOfSightToThing(req.DestinationCell.Value, req.Target, map, false, c => LOSValidator(c, map)))
                return new GrappleAttemptReport(req, "MissingLOS");

            // Success!
            return GrappleAttemptReport.Success(req.DestinationCell.Value);
        }


        // Get free spots around pawn for grapple.
        int spotCount = req.OccupiedMask == null ?
                        UpdateClosestCells(req, map) :
                        UpdateClosestCells(req, req.OccupiedMask.Value);
        if (spotCount == 0)
            return new GrappleAttemptReport(req, "NoDest");

        // Check LOS.
        for (int i = 0; i < spotCount; i++)
        {
            var cell = closestCells[i];
            if (GenSight.LineOfSightToThing(cell, req.Target, map, false, c => LOSValidator(c, map)))
            {
                // Success!
                return GrappleAttemptReport.Success(cell);
            }
        }

        return new GrappleAttemptReport(req, "MissingLOS");
    }

    public IEnumerable<ExecutionAttemptReport> GetExecutionReport(ExecutionAttemptRequest req)
    {
        if (req.Executioner == null)
            yield break;

        if (req.Target == null && (req.Targets == null || !req.Targets.Any()))
            yield break;

        // Missing weapon.
        var weapon = req.Executioner.GetFirstMeleeWeapon();
        if (weapon == null)
        {
            yield return new ExecutionAttemptReport(req, "NoWeapon");
            yield break;
        }

        // Check spawned.
        if (!req.Executioner.Spawned)
        {
            yield return new ExecutionAttemptReport(req, "Internal", "Executioner not spawned");
            yield break;
        }

        // Check downed.
        if (req.Executioner.Downed)
        {
            yield return new ExecutionAttemptReport(req, "Downed");
            yield break;
        }

        // Check if grappler is in an animation.
        if (req.Executioner.IsInAnimation())
        {
            yield return new ExecutionAttemptReport(req, "SelfInAnimation");
            yield break;
        }

        var data = req.Executioner.GetMeleeData();

        // Check cooldown.
        if (!req.IgnoreCooldown && !data.IsExecutionOffCooldown())
        {
            yield return new ExecutionAttemptReport(req, "Cooldown");
            yield break;
        }

        // Get all animations.
        var allAnims = req.OnlyTheseAnimations ?? AnimDef.GetExecutionAnimationsForPawnAndWeapon(req.Executioner, weapon.def).Where(d => d.Probability > 0);
        if (!allAnims.Any())
        {
            yield return new ExecutionAttemptReport(req, "NoAnims");
            yield break;
        }

        GetPossibleAdjacentExecutions(req, allAnims);
        var ep = req.Executioner.Position;

        // Check if the lasso can be used by getting a generic report.
        bool canUseLasso = req.CanUseLasso;
        if (canUseLasso)
        {
            var genericLassoReport = GetGrappleReport(new GrappleAttemptRequest
            {
                Grappler = req.Executioner,
                OccupiedMask = req.SmallOccupiedMask,
                NoErrorMessages = true,
                TrustLassoUsability = req.TrustLassoUsability,
                LassoRange = req.LassoRange
            });
            if (!genericLassoReport.CanGrapple)
            {
                canUseLasso = false;
            }
        }

        ExecutionAttemptReport Process(Pawn target)
        {
            // Dead or downed.
            if (target.Dead)
                return new ExecutionAttemptReport(req, "DeadTarget");
            if (target.Downed)
                return new ExecutionAttemptReport(req, "DownedTarget");

            // Animal when not allowed.
            if (!Core.Settings.AnimalsCanBeExecuted && target.def.race.Animal)
                return new ExecutionAttemptReport(req, "Internal", "Animals not allowed"); // Internal error because the UI should do the animal filtering.
            
            // In animation.
            if (target.IsInAnimation())
                return new ExecutionAttemptReport(req, "InAnimation");

            // Make report assuming something will work out...
            var report = new ExecutionAttemptReport
            {
                Target = target,
                PossibleExecutions = ExecutionAttemptReport.BorrowList(),
                CanExecute = true
            };
            report.PossibleExecutions.Clear();

            // Check immediately adjacent.
            var tp = target.Position;
            if (tempStartDataList.Count > 0 && tp.z == ep.z)
            {
                bool east = tp.x == ep.x + 1;
                bool west = tp.x == ep.x - 1;

                if (west)
                {
                    foreach (var start in tempStartDataList)
                    {
                        if (start.FlipX)
                        {
                            report.PossibleExecutions.Add(new PossibleExecution
                            {
                                Animation = start,
                                LassoToHere = null
                            });
                        }
                    }

                    // If anything can be done immediately, it takes priority over anything else.
                    if (report.PossibleExecutions.Count > 0)
                        return report;
                }
                else if (east)
                {
                    foreach (var start in tempStartDataList)
                    {
                        if (!start.FlipX)
                        {
                            report.PossibleExecutions.Add(new PossibleExecution
                            {
                                Animation = start,
                                LassoToHere = null
                            });
                        }
                    }

                    // If anything can be done immediately, it takes priority over anything else.
                    if (report.PossibleExecutions.Count > 0)
                        return report;
                }

                // If the target is directly adjacent but still could not be executed,
                // it must be because there was no space to perform any animation there.
                if (east || west)
                {
                    report.Dispose();
                    return new ExecutionAttemptReport(req, "NoSpace");
                }
            }

            // Can the lasso be used?
            if (canUseLasso)
            {
                // Generate a report.
                var lassoReport = GetGrappleReport(new GrappleAttemptRequest
                {
                    DoNotCheckCooldown = true,
                    DoNotCheckLasso = true,
                    Grappler = req.Executioner,
                    GrappleSpotPickingBehaviour = GrappleSpotPickingBehaviour.OnlyAdjacent,
                    OccupiedMask = req.SmallOccupiedMask,
                    Target = target,
                    NoErrorMessages = true,
                    TrustLassoUsability = req.TrustLassoUsability,
                    LassoRange = req.LassoRange
                });

                // The lasso can bring them into range (directly adjacent)!
                if (lassoReport.CanGrapple)
                {
                    bool flipX = lassoReport.DestinationCell.x < ep.x;

                    // Find all execution animations that can be done from the grapple side.
                    foreach (var start in tempStartDataList)
                    {
                        if (start.FlipX == flipX)
                        {
                            report.PossibleExecutions.Add(new PossibleExecution
                            {
                                Animation = start,
                                LassoToHere = lassoReport.DestinationCell
                            });
                        }
                    }

                    // If any executions can be done, then this is the optimal execution route.
                    if (report.PossibleExecutions.Count > 0)
                    {
                        return report;
                    }
                }
            }

            // Is the executioner allowed to walk to the target?
            if (!req.CanWalk)
            {
                report.Dispose();
                return new ExecutionAttemptReport(req, "NoWalk");
            }

            // The target is not adjacent and the lasso cannot be used.
            // Must walk...
            bool canReach = req.Executioner.CanReach(target, PathEndMode.Touch, Danger.Deadly);
            if (!canReach)
            {
                // There is absolutely no way to reach the target by walking.
                // Oh well.
                report.Dispose();
                return new ExecutionAttemptReport(req, "NoPath");
            }

            // Okay, attempt to walk + execute.
            return report;
        }

        if (req.Target != null)
        {
            yield return Process(req.Target);
        }

        if (req.Targets == null)
            yield break;

        foreach (var target in req.Targets)
        {
            yield return Process(target);
        }
        
    }

    public DuelAttemptReport GetDuelReport(in DuelAttemptRequest req)
    {
        bool CheckPawn(Pawn pawn, out string cat, out string msg, in DuelAttemptRequest req)
        {
            msg = null;

            // Null.
            if (pawn == null)
            {
                cat = "Internal";
                msg = "Pawn is null.";
                return false;
            }

            // Not spawned.
            if (!pawn.Spawned)
            {
                cat = "NotSpawned";
                return false;
            }

            // Dead or downed.
            if (pawn.Dead)
            {
                cat = "Dead";
                return false;
            }
            if (pawn.Downed)
            {
                cat = "Downed";
                return false;
            }

            // On Cooldown
            if (req.IsFriendly)
            {
                if (!pawn.GetMeleeData().IsFriendlyDuelOffCooldown())
                {
                    cat = "OnCooldown";
                    return false;
                }
            }
            else
            {
                // TODO non-friendly duel cooldown here.
            }

            // In an animation.
            if (pawn.TryGetAnimator() != null)
            {
                cat = "NotSpawned"; // The message is quite generic.
                return false;
            }

            // Being lassoed.
            if (GrabUtility.IsBeingTargetedForGrapple(pawn))
            {
                cat = "NotSpawned";
                return false;
            }

            cat = null;
            return true;
        }

        // Check both pawns for basic stuff:
        if (!CheckPawn(req.A, out var cat, out var msg, req))
            return new DuelAttemptReport(req, cat, msg, true);
        if (!CheckPawn(req.B, out cat, out msg, req))
            return new DuelAttemptReport(req, cat, msg, false);

        // Check adjacent:
        var posA = req.A.Position;
        var posB = req.B.Position;
        bool areAdjacent = posA.z == posB.z && Mathf.Abs(posA.x - posB.x) == 1;

        // Check melee weapons:
        var weaponA = req.A.GetFirstMeleeWeapon();
        var weaponB = req.B.GetFirstMeleeWeapon();

        if (weaponA == null)
            return new DuelAttemptReport(req, "MissingWeapon", pawnA: true);
        if (weaponB == null)
            return new DuelAttemptReport(req, "MissingWeapon", pawnA: false);

        // Try to find an animation that those weapons could do:
        var duelDef = TryGetDuelAnimationFor(weaponA, weaponB, out bool? mustPivotOnA);
        if (duelDef == null)
            return new DuelAttemptReport(req, "NoAnim");

        // Now that we have an animation, if the pawns are adjacent the this is a success!
        if (areAdjacent)
        {
            // Success, immediate duel start.
            return new DuelAttemptReport
            {
                CanStartDuel = true,
                MustWalk = false,
                DuelAnimation = duelDef,
                CenteredOnPawn = mustPivotOnA == null ? null : mustPivotOnA.Value ? req.A : req.B
            };
        }

        // There is an animation and everything else is ready to go, but the pawn must walk.
        // Check the pathfinding.
        bool canReach = req.A.CanReach(req.B, PathEndMode.Touch, Danger.Deadly);
        if (!canReach)
            return new DuelAttemptReport(req, "NoPath");

        // Success, but must walk and make another request once the target has been reached.
        return new DuelAttemptReport
        {
            CanStartDuel = true,
            DuelAnimation = duelDef,
            CenteredOnPawn = null, // No point in specifying, another request will need to be made upon arrival
            MustWalk = true
        };
    }

    private void GetPossibleAdjacentExecutions(ExecutionAttemptRequest args, IEnumerable<AnimDef> animDefs)
    {
        tempStartDataList.Clear();

        if (!args.EastCell && !args.WestCell)
            return;

        var weapon = args.Executioner?.GetFirstMeleeWeapon();
        if (weapon == null)
            return;

        foreach (var anim in animDefs)
        {
            if (args.WestCell)
            {
                ulong animMask = anim.FlipClearMask;
                if ((animMask & args.OccupiedMask) == 0)
                {
                    tempStartDataList.Add(new PossibleExecution.AnimStartData
                    {
                        AnimDef = anim,
                        FlipX = true
                    });
                }
            }
            if (args.EastCell)
            {
                ulong animMask = anim.ClearMask;
                if ((animMask & args.OccupiedMask) == 0)
                {
                    tempStartDataList.Add(new PossibleExecution.AnimStartData
                    {
                        AnimDef = anim,
                        FlipX = false
                    });
                }
            }
        }
    }

    private int UpdateClosestCells(in GrappleAttemptRequest req, Map map)
    {
        int count = 0;
        var center = req.Grappler.Position;

        void Add(in IntVec3 cell)
        {
            if (CanStandHere(map, cell))
                closestCells[count++] = cell;
        }

        // Populate the array.
        // Left and right.
        Add(center + new IntVec3(-1, 0,  0));
        Add(center + new IntVec3( 1, 0,  0));

        if (req.GrappleSpotPickingBehaviour != GrappleSpotPickingBehaviour.OnlyAdjacent)
        {
            Add(center + new IntVec3(0, 0, -1));
            Add(center + new IntVec3(0, 0, 1));
            Add(center + new IntVec3(-1, 0, 1));
            Add(center + new IntVec3(-1, 0, -1));
            Add(center + new IntVec3(1, 0, 1));
            Add(center + new IntVec3(1, 0, -1));
        }

        // Sort.
        if (req.Target != null)
        {
            comparer.TargetPos = req.Target.Position;
            comparer.CenterZ = center.z;
            comparer.PreferAdjacent = req.GrappleSpotPickingBehaviour != GrappleSpotPickingBehaviour.Closest;
            Array.Sort(closestCells, 0, count, comparer);
        }

        return count;
    }

    private int UpdateClosestCells(in GrappleAttemptRequest req, uint occupiedMask)
    {
        int count = 0;
        var center = req.Grappler.Position;
        var maskStartCell = center - new IntVec3(1, 0, 1);

        void Add(in IntVec3 cell)
        {
            int index = (cell.x - maskStartCell.x) + (cell.z - maskStartCell.z) * 3;

            if ((occupiedMask & (1 << index)) == 0)
                closestCells[count++] = cell;
        }

        // Populate the array.
        // Left and right.
        Add(center + new IntVec3(-1, 0, 0));
        Add(center + new IntVec3(1, 0, 0));

        if (req.GrappleSpotPickingBehaviour != GrappleSpotPickingBehaviour.OnlyAdjacent)
        {
            Add(center + new IntVec3(0, 0, -1));
            Add(center + new IntVec3(0, 0, 1));
            Add(center + new IntVec3(-1, 0, 1));
            Add(center + new IntVec3(-1, 0, -1));
            Add(center + new IntVec3(1, 0, 1));
            Add(center + new IntVec3(1, 0, -1));
        }

        // Sort (if a target is specified)
        if (req.Target != null)
        { 
            comparer.TargetPos = req.Target.Position;
            comparer.CenterZ = center.z;
            comparer.PreferAdjacent = req.GrappleSpotPickingBehaviour != GrappleSpotPickingBehaviour.Closest;
            Array.Sort(closestCells, 0, count, comparer);
        }
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanStandHere(Map map, in IntVec3 cell)
    {
        // TODO make much faster version.
        return cell.Standable(map);
    }

    public static AnimDef TryGetDuelAnimationFor(Thing a, Thing b, out bool? focusOnA)
    {
        var inA = new ReqInput(a.def);
        var inB = new ReqInput(b.def);

        foreach (var anim in AnimDef.GetDefsOfType(AnimType.Duel))
        {
            if (anim.weaponFilter == null)
                continue;

            bool isSingleFilter = anim.weaponFilterSecond == null;

            // Single filter for both weapons:
            if (isSingleFilter)
            {
                // Both weapons must match the filter:
                if (anim.weaponFilter.Evaluate(inA) && anim.weaponFilter.Evaluate(inB))
                {
                    focusOnA = null;
                    return anim;
                }
                continue;
            }

            // Dual filter:
            // Both filters need to be checked, and flipped.
            bool Eval(in ReqInput ra, in ReqInput rb) => anim.weaponFilter.Evaluate(ra) && anim.weaponFilterSecond.Evaluate(rb);

            if (Eval(inA, inB))
            {
                if (Eval(inB, inA))
                {
                    // Can be flipped.
                    focusOnA = null;
                    return anim;
                }

                // Cannot be flipped.
                focusOnA = true;
                return anim;
            }

            if (Eval(inB, inA))
            {
                // Cannot be flipped.
                focusOnA = false;
                return anim;
            }
        }

        focusOnA = null;
        return null;
    }

    private class Comparer : IComparer<IntVec3>
    {
        public IntVec3 TargetPos;
        public int CenterZ;
        public bool PreferAdjacent;

        private float GetDistance(in IntVec3 cell)
        {
            float dst = cell.DistanceTo(TargetPos); // TODO optimize.

            if (PreferAdjacent && CenterZ == cell.z)
                return dst * 0.01f;

            return dst;
        }

        public int Compare(IntVec3 x, IntVec3 y)
        {
            return GetDistance(x).CompareTo(GetDistance(y));
        }
    }
}

public enum GrappleSpotPickingBehaviour
{
    /// <summary>
    /// The first cell that minimizes the target travel distance will be picked.
    /// </summary>
    Closest,

    /// <summary>
    /// Directly adjacent cells (west and east) will be preferred over any other cell,
    /// but if those are not viable then the behaviour of <see cref="Closest"/> is used.
    /// </summary>
    PreferAdjacent,

    /// <summary>
    /// Directly adjacent cells (west and east) are requested and if they are not viable the request fails.
    /// </summary>
    OnlyAdjacent
}
