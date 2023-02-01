using AAM.Grappling;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Verse;

namespace AAM;

public class ActionController
{
    private readonly IntVec3[] closestCells = new IntVec3[8];
    private readonly Comparer comparer = new Comparer();

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

        var data = req.Grappler.GetMeleeData();

        // Check lasso cooldown.
        if (!req.DoNotCheckCooldown && !data.IsGrappleOffCooldown())
            return new GrappleAttemptReport(req, "Cooldown");

        // Check lasso.
        if (!req.DoNotCheckLasso && req.Grappler.TryGetLasso() == null)
            return new GrappleAttemptReport(req, "MissingLasso");

        // Check skill.
        if (Core.Settings.MinMeleeSkillToLasso > 0)
        {
            int meleeSkill = req.Grappler.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
            if (meleeSkill < Core.Settings.MinMeleeSkillToLasso)
                return new GrappleAttemptReport(req, "SkillIssue");
        }

        // Check manipulation.
        if (Core.Settings.MinManipulationToLasso > 0)
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
        float range = req.Grappler.GetStatValue(AAM_DefOf.AAM_GrappleRadius);
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
                Core.Log("Have LOS to " + cell + " from " + req.Target);
                // Success!
                return GrappleAttemptReport.Success(cell);
            }
        }

        return new GrappleAttemptReport(req, "MissingLOS");
    }

    /// <summary>
    /// Enumerates all possible animations that can be performed by the <see cref="PossibleExecutionRequestArgs.Executioner"/>
    /// at their current position using their current melee weapon, ensuring that there is enough space to perform the animation.
    /// Does not check any other factors such as health, cooldown etc.
    /// </summary>
    public IEnumerable<PossibleExecution> GetPossibleExecutions(PossibleExecutionRequestArgs args)
    {
        if (!args.EastCell && !args.WestCell)
            yield break;

        var weapon = args.Executioner?.GetFirstMeleeWeapon();
        if (weapon == null)
            yield break;

        var allAnimations = AnimDef.GetExecutionAnimationsForPawnAndWeapon(args.Executioner, weapon.def).Where(d => d.Probability > 0);
        foreach (var anim in allAnimations)
        {
            if (args.WestCell)
            {
                ulong animMask = anim.FlipClearMask;
                if ((animMask & args.OccupiedMask) == 0)
                {
                    yield return new PossibleExecution
                    {
                        Animation = anim,
                        FlipX = true
                    };
                }
            }
            if (args.EastCell)
            {
                ulong animMask = anim.ClearMask;
                if ((animMask & args.OccupiedMask) == 0)
                {
                    yield return new PossibleExecution
                    {
                        Animation = anim,
                        FlipX = false
                    };
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
        comparer.TargetPos = req.Target.Position;
        comparer.CenterZ = center.z;
        comparer.PreferAdjacent = req.GrappleSpotPickingBehaviour != GrappleSpotPickingBehaviour.Closest;
        Array.Sort(closestCells, 0, count, comparer);

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

        // Sort.
        comparer.TargetPos = req.Target.Position;
        comparer.CenterZ = center.z;
        comparer.PreferAdjacent = req.GrappleSpotPickingBehaviour != GrappleSpotPickingBehaviour.Closest;
        Array.Sort(closestCells, 0, count, comparer);

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

public struct PossibleExecutionRequestArgs
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
    /// Should the west cell be checked?
    /// </summary>
    public bool WestCell;
    /// <summary>
    /// Should the east cell be checked?
    /// </summary>
    public bool EastCell;
}

public struct PossibleExecution
{
    public float Probability => Animation.Probability;

    /// <summary>
    /// The animation to be played.
    /// </summary>
    public AnimDef Animation;
    /// <summary>
    /// Whether the animation needs to be mirrored horizontally.
    /// </summary>
    public bool FlipX;
}
