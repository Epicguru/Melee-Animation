using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using UnityEngine;
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

        // You can add a bound pawn texture here for your modded body type.
        // Alternatively, simply add your texture to your textures folder: Textures/AAM/BoundPawns/<bodyTypeDefName>.png
        public static readonly Dictionary<BodyTypeDef, Texture2D> BoundPawnTextures = new Dictionary<BodyTypeDef, Texture2D>();

        private static readonly List<AnimDef> tempAnimations = new List<AnimDef>();
        private static readonly HashSet<IntVec2> tempCells = new HashSet<IntVec2>();
        private static MaterialPropertyBlock mpb = new MaterialPropertyBlock();

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

                // TODO check all cells: make centralized method.
                if (tempCells.Contains(start.Value))
                {
                    yield return new PossibleExecution()
                    {
                        Def = anim,
                        Victim = pawn,
                        VictimMoveCell = pawn.Position == start.Value.ToIntVec3 ? null : start.Value.ToIntVec3,
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

                if (SpaceChecker.IsValidPawnPosFast(map, size.x, size.z, pos))
                    yield return pos;
            }
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

        public static void DrawRopeFromTo(Vector3 from, Vector3 to, Color ropeColor)
        {
            float len = (to - from).magnitude;
            float angle = (to - from).AngleFlat();
            Vector3 mid = (from + to) * 0.5f;

            mpb.SetTexture("_MainTex", Content.Rope); // TODO cache id.
            mpb.SetVector("_MainTex_ST", new Vector4(len, 1, 0, 0));
            mpb.SetColor("_Color", ropeColor);

            // Line.
            float ratio = (float)Content.Rope.height / Content.Rope.width;
            var mat = AnimRenderer.DefaultCutout;
            var trs = Matrix4x4.TRS(mid, Quaternion.Euler(0f, angle + 90f, 0f), new Vector3(len, 1f, ratio));
            Graphics.DrawMesh(MeshPool.plane10, trs, mat, 0, null, 0, mpb);

            // Coil.
            from.y += 0.0001f;
            mpb.SetTexture("_MainTex", Content.RopeCoil); // TODO cache id.
            mpb.SetVector("_MainTex_ST", new Vector4(1, 1, 0, 0));
            trs = Matrix4x4.TRS(from, Quaternion.Euler(0f, angle + 90f, 0f), Vector3.one * 0.3f);
            Graphics.DrawMesh(MeshPool.plane10, trs, mat, 0, null, 0, mpb);

            // Dangle.
            from.y -= 0.0005f;
            mpb.SetTexture("_MainTex", Content.RopeEnd); // TODO cache id.
            trs = Matrix4x4.TRS(from, Quaternion.identity, Vector3.one * 1);
            Graphics.DrawMesh(MeshPool.plane10, trs, mat, 0, null, 0, mpb);
        }

        public static Texture2D GetBoundPawnTexture(Pawn pawn)
        {
            BodyTypeDef btd = pawn?.story?.bodyType;
            if (btd == null)
                return null;

            return GetBoundPawnTexture(pawn, btd);
        }

        private static Texture2D GetBoundPawnTexture(Pawn pawn, BodyTypeDef bodyType)
        {
            if (BoundPawnTextures.TryGetValue(bodyType, out var found))
                return found;

            var loaded = ContentFinder<Texture2D>.Get($"AAM/BoundPawns/{bodyType.defName}");
            if (loaded != null)
                BoundPawnTextures.Add(bodyType, loaded);
            return loaded;
        }
    }
}
