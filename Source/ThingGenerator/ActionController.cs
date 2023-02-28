using AAM.Grappling;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Verse;
using Verse.AI;
using static AAM.PossibleExecution;

namespace AAM;

public class ActionController
{
    private readonly IntVec3[] closestCells = new IntVec3[8];
    private readonly Comparer comparer = new Comparer();
    private readonly List<AnimStartData> tempStartDataList = new List<AnimStartData>(64);

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
        float range = req.LassoRange ?? req.Grappler.GetStatValue(AAM_DefOf.AAM_GrappleRadius);
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
            if (!GenSight.LineOfSightToThing(req.DestinationCell.Value, req.Target, map))
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
            if (GenSight.LineOfSightToThing(cell, req.Target, map))
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
        if (!data.IsExecutionOffCooldown())
        {
            yield return new ExecutionAttemptReport(req, "Cooldown");
            yield break;
        }

        // Get all animations.
        var allAnims = AnimDef.GetExecutionAnimationsForPawnAndWeapon(req.Executioner, weapon.def).Where(d => d.Probability > 0);
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
                    tempStartDataList.Add(new AnimStartData
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
                    tempStartDataList.Add(new AnimStartData
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

public struct GrappleAttemptRequest
{
    /// <summary>
    /// The pawn to do the grappling.
    /// </summary>
    public Pawn Grappler;

    /// <summary>
    /// The target pawn to be grappled.
    /// </summary>
    public Pawn Target;

    /// <summary>
    /// The optional target cell for the grapple.
    /// It is assumed to be adjacent to the pawn.
    /// </summary>
    public IntVec3? DestinationCell;

    /// <summary>
    /// If <see cref="DestinationCell"/> is null,
    /// this defines the destination cell selection algorithm.
    /// </summary>
    public GrappleSpotPickingBehaviour GrappleSpotPickingBehaviour;

    /// <summary>
    /// If true, the <see cref="Grappler"/> is not checked for a lasso.
    /// </summary>
    public bool DoNotCheckLasso;

    /// <summary>
    /// If true, the <see cref="Grappler"/> is not checked for lasso cooldown.
    /// </summary>
    public bool DoNotCheckCooldown;

    /// <summary>
    /// If true, the <see cref="GrappleAttemptReport.ErrorMessage"/> is not populated, to save time.
    /// </summary>
    public bool NoErrorMessages;

    /// <summary>
    /// The optional occupied mask around the <see cref="Grappler"/>.
    /// If supplied, it speeds up the operation by avoiding checking the map constantly.
    /// </summary>
    public uint? OccupiedMask;

    /// <summary>
    /// If true, most lasso checks are skipped (such as melee skill, manipulation etc.)
    /// </summary>
    public bool TrustLassoUsability;

    /// <summary>
    /// Allows the specification of lasso range. If null, it is extracted from the <see cref="Grappler"/>'s stats.
    /// </summary>
    public float? LassoRange;
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

public struct GrappleAttemptReport
{
    private static readonly NamedArgument[] namedArgs = new NamedArgument[4];

    public static GrappleAttemptReport Success(IntVec3 destinationCell) => new GrappleAttemptReport
    {
        CanGrapple = true,
        DestinationCell = destinationCell
    };

    private static NamedArgument[] GetNamedArgs(in GrappleAttemptRequest request, string intErrorMsg)
    {
        namedArgs[0] = new NamedArgument(request.Grappler, "Grappler");
        namedArgs[1] = new NamedArgument(request.Target, "Target");
        namedArgs[2] = new NamedArgument(intErrorMsg, "Error");
        namedArgs[3] = new NamedArgument(Core.Settings.MinMeleeSkillToLasso, "MeleeSkill");
        return namedArgs;
    }

    private static string GetShortTrs(string trsKey, string trs, NamedArgument[] args)
    {
        string shortKey = $"{trsKey}.Short";
        if (shortKey.CanTranslate())
            return shortKey.Translate(args);
        return trs;
    }

    /// <summary>
    /// Can the grapple be started?
    /// </summary>
    public bool CanGrapple;

    /// <summary>
    /// The cell that the target must be grappled to.
    /// If <see cref="CanGrapple"/> is false, this value holds no meaning.
    /// </summary>
    public IntVec3 DestinationCell;

    /// <summary>
    /// If <see cref="CanGrapple"/> is false this is the reason why.
    /// </summary>
    public string ErrorMessage;

    /// <summary>
    /// If <see cref="CanGrapple"/> is false this is the reason why (short version).
    /// </summary>
    public string ErrorMessageShort;

    public GrappleAttemptReport(in GrappleAttemptRequest req, string errorTrsKey, string intErrorMsg = null)
    {
        if (errorTrsKey == null)
            throw new ArgumentNullException(nameof(errorTrsKey));

        CanGrapple = false;
        DestinationCell = default;

        if (req.NoErrorMessages)
        {
            ErrorMessage = null;
            ErrorMessageShort = null;
        }
        else
        {
            var args = GetNamedArgs(req, intErrorMsg);
            errorTrsKey = $"AAM.Error.Grapple.{errorTrsKey}";
            ErrorMessage = errorTrsKey.Translate(args);
            ErrorMessageShort = intErrorMsg == null ? GetShortTrs(errorTrsKey, ErrorMessage, args) : ErrorMessage;
        }
    }
}

public struct ExecutionAttemptRequest
{
    /// <summary>
    /// The executioner pawn.
    /// </summary>
    public Pawn Executioner;
    /// <summary>
    /// The occupied mask for the <see cref="Executioner"/>.
    /// </summary>
    public ulong OccupiedMask;
    /// <summary>
    /// The small occupied mask for the executioner.
    /// </summary>
    public uint SmallOccupiedMask;
    /// <summary>
    /// Should the west cell be checked?
    /// </summary>
    public bool WestCell;
    /// <summary>
    /// Should the east cell be checked?
    /// </summary>
    public bool EastCell;
    /// <summary>
    /// Is the <see cref="Executioner"/> allowed to use their lasso (if any)?
    /// </summary>
    public bool CanUseLasso;
    /// <summary>
    /// Is the executioner allowed to walk to the target(s) to execute them?
    /// </summary>
    public bool CanWalk;
    /// <summary>
    /// If true, most lasso checks are skipped (such as melee skill, manipulation etc.)
    /// Only valid if <see cref="CanUseLasso"/> is true.
    /// </summary>
    public bool TrustLassoUsability;

    /// <summary>
    /// Allows the specification of lasso range. If null, it is extracted from the <see cref="Executioner"/>'s stats.
    /// </summary>
    public float? LassoRange;

    /// <summary>
    /// The main target.
    /// </summary>
    public Pawn Target;

    /// <summary>
    /// Optional additional targets.
    /// </summary>
    public IEnumerable<Pawn> Targets;

    /// <summary>
    /// If true no error messages will be translated to save time.
    /// </summary>
    public bool NoErrorMessages;
}

public struct PossibleExecution
{
    public bool IsValid => Animation.AnimDef != null;

    /// <summary>
    /// Info about the animation to be started.
    /// </summary>
    public AnimStartData Animation;

    /// <summary>
    /// If not null, the pawn must be lassoed to this position in order for the execution to happen.
    /// </summary>
    public IntVec3? LassoToHere;

    public struct AnimStartData
    {
        public float Probability => AnimDef?.Probability ?? 0;

        /// <summary>
        /// The animation to be played.
        /// </summary>
        public AnimDef AnimDef;

        /// <summary>
        /// Whether the animation needs to be mirrored horizontally.
        /// </summary>
        public bool FlipX;
    }
}

public struct ExecutionAttemptReport : IDisposable
{
    private static readonly Queue<List<PossibleExecution>> listPool = new Queue<List<PossibleExecution>>(32);
    private static readonly NamedArgument[] namedArgs = new NamedArgument[3];

    public static List<PossibleExecution> BorrowList()
    {
        lock (listPool)
        {
            if (listPool.Count > 0)
                return listPool.Dequeue();
        }
        return new List<PossibleExecution>(32);
    }

    private static void ReturnList(List<PossibleExecution> list)
    {
        lock (listPool)
        {
            listPool.Enqueue(list);
        }
    }

    private static NamedArgument[] GetNamedArgs(in ExecutionAttemptRequest request, Pawn target, string intErrorMsg)
    {
        namedArgs[0] = new NamedArgument(request.Executioner, "Exec");
        namedArgs[1] = new NamedArgument(target, "Target");
        namedArgs[2] = new NamedArgument(intErrorMsg, "Error");
        return namedArgs;
    }

    private static string GetShortTrs(string trsKey, string trs, NamedArgument[] args)
    {
        string shortKey = $"{trsKey}.Short";
        if (shortKey.CanTranslate())
            return shortKey.Translate(args);
        return trs;
    }

    /// <summary>
    /// If true, indicates that this report means that the executioner cannot perform any execution,
    /// such as because they are not holding a weapon or their execution is on cooldown.
    /// </summary>
    public bool IsFinal => !CanExecute && Target == null;

    /// <summary>
    /// Is any execution possible?
    /// </summary>
    public bool CanExecute;

    /// <summary>
    /// An enumeration of possible executions.
    /// Will be null if <see cref="CanExecute"/> is false.
    /// </summary>
    public List<PossibleExecution> PossibleExecutions;

    /// <summary>
    /// The pawn that could be executed.
    /// </summary>
    public Pawn Target;

    public bool IsWalking => CanExecute && (PossibleExecutions == null || !PossibleExecutions.Any());

    public string ErrorMessage;

    public string ErrorMessageShort;

    public ExecutionAttemptReport(in ExecutionAttemptRequest req, string errorTrsKey, string intErrorMsg = null)
        : this(req, null, errorTrsKey, intErrorMsg) { }
    
    public ExecutionAttemptReport(in ExecutionAttemptRequest req, Pawn target, string errorTrsKey, string intErrorMsg = null)
    {
        CanExecute = false;
        PossibleExecutions = null;
        Target = target;

        if (req.NoErrorMessages)
        {
            ErrorMessage = null;
            ErrorMessageShort = null;
        }
        else
        {
            var args = GetNamedArgs(req, target, intErrorMsg);
            errorTrsKey = $"AAM.Error.Exec.{errorTrsKey}";
            ErrorMessage = errorTrsKey.Translate(args);
            ErrorMessageShort = intErrorMsg == null ? GetShortTrs(errorTrsKey, ErrorMessage, args) : ErrorMessage;
        }
    }

    public void Dispose()
    {
        if (PossibleExecutions == null)
            return;

        var temp = PossibleExecutions;
        PossibleExecutions = null;
        ReturnList(temp);
    }
}
