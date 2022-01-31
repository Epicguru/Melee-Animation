using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace AAM.Grappling
{
    public static class GrabUtility
    {
        /*
         * Strategy to grab pawns:
         * 1. Get a list of pawns we can grab (be that short or long range)
         * 2. Make a list of spots around us that we can drag pawns into.
         * 3. Randomize list of grabbable pawns.
         * 4. For each pawn, see what executions we can perform with our current weapon.
         * 5. If that execution animation has free spots around executioner, then that animation can be played.
         */

        public struct PossibleExecution
        {
            public AnimDef Def;
            public Pawn Victim;
            public IntVec3? VictimMoveCell;
            public bool MirrorX, MirrorY;
        }

        private static readonly List<AnimDef> tempAnimations = new List<AnimDef>();
        private static readonly HashSet<IntVec2> tempCells = new HashSet<IntVec2>();

        public static IEnumerable<PossibleExecution> GetPossibleExecutions(Pawn executioner, IEnumerable<Pawn> targetPawns)
        {
            // In the interest of speed, target pawns are not validated.
            // The executioner is also assumed to be spawned, not dead, and in the same map as all the target pawns.

            var weapon = executioner.GetFirstMeleeWeapon();
            if (weapon == null)
                yield break; // No melee weapon, no executions...

            // Populate the list of possible animations.
            tempAnimations.Clear();
            tempAnimations.AddRange(AnimDef.GetExecutionAnimationsForWeapon(weapon.def));

            if (tempAnimations.Count == 0)
                yield break; // No animations to play.

            // Populate the list of free cells around the executioner.
            tempCells.Clear();
            tempCells.AddRange(GetFreeSpotsAround(executioner).Select(v3 => v3.ToIntVec2));

            // Cache the executioner pos.
            var execPos = new IntVec2(executioner.Position.x, executioner.Position.z);

            IEnumerable<PossibleExecution> CheckAnim(Pawn pawn, AnimDef anim, bool fx, bool fy)
            {
                var start = anim.TryGetCell(AnimCellData.Type.PawnStart, fx, fy, 1);
                if (start == null)
                    yield break;

                var end = anim.TryGetCell(AnimCellData.Type.PawnEnd, fx, fy, 1) ?? start.Value;

                start += execPos;
                end += execPos;

                if (tempCells.Contains(start.Value) && tempCells.Contains(end))
                {
                    yield return new PossibleExecution()
                    {
                        Def = anim,
                        Victim = pawn,
                        VictimMoveCell = pawn.Position == end.ToIntVec3 ? null : end.ToIntVec3,
                        MirrorX = fx,
                        MirrorY = fy
                    };
                }
            }

            foreach (var pawn in targetPawns)
            {
                foreach (var anim in tempAnimations)
                {
                    switch (anim.direction)
                    {
                        case AnimDirection.Horizontal:

                            foreach (var exec in CheckAnim(pawn, anim, false, false)) yield return exec;
                            foreach (var exec in CheckAnim(pawn, anim, true, false)) yield return exec;

                            break;

                        case AnimDirection.North or AnimDirection.South:
                            foreach (var exec in CheckAnim(pawn, anim, false, false)) yield return exec;
                            break;

                        default:
                            Core.Error($"Unhandled animation direction: {anim.direction}");
                            break;
                    }
                }
            }
        }

        public static IEnumerable<IntVec3> GetFreeSpotsAround(Pawn pawn)
        {
            if (pawn == null)
                yield break;

            var basePos = pawn.Position;
            var map = pawn.Map;
            var size = map.Size;

            foreach (var offset in GenAdj.AdjacentCellsAround)
            {
                var pos = basePos + offset;
                if (pos.x < 0 || pos.z < 0 || pos.x >= size.x || pos.z >= size.z)
                    continue;

                if (IsValidPawnPosFast(map, pos))
                    yield return pos;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsValidPawnPosFast(in Map map, in IntVec3 cell)
        {
            // Based on Verb_Jump.ValidJumpTarget but does not consider map bounds since those have already been checked.
            if (cell.Impassable(map) || !cell.Walkable(map))
                return false;
            
            Building edifice = cell.GetEdifice(map);
            Building_Door building_Door;
            return edifice == null || (building_Door = (edifice as Building_Door)) == null || building_Door.Open;
        }

        public static IEnumerable<Pawn> GetGrabbablePawnsAround(Pawn pawn)
        {
            if (pawn == null)
                yield break;

            var map = pawn.Map;
            var pos = pawn.Position;
            var size = map.Size;

            foreach (var offset in GenAdj.AdjacentCellsAround)
                foreach (var p in TryGetPawnsGrabbableBy(map, size, pawn, pos + offset))
                    yield return p;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static IEnumerable<Pawn> TryGetPawnsGrabbableBy(Map map, IntVec3 mapSize, Pawn pawn, IntVec3 pos)
        {
            if (pos.x < 0 || pos.z < 0 || pos.x >= mapSize.x || pos.z >= mapSize.z)
                yield break;

            var things = map.thingGrid.ThingsListAtFast(pos);
            foreach (var thing in things)
            {
                if (thing is Pawn p && CanGrabPawn(pawn, p))
                    yield return p;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CanGrabPawn(Pawn a, Pawn b) => !b.Dead && !b.Downed && b.Spawned;
    }
}
