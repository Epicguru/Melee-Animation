using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace AM.Grappling
{
    public static class GrabUtility
    {
        // You can add a bound pawn texture here for your modded body type.
        // Alternatively, simply add your texture to your textures folder: Textures/AM/BoundPawns/<bodyTypeDefName>.png
        public static readonly Dictionary<BodyTypeDef, Texture2D> BoundPawnTextures = new Dictionary<BodyTypeDef, Texture2D>();

        private static readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        private static readonly HashSet<Pawn> pawnsBeingTargetedByGrapples = new HashSet<Pawn>();

        public static bool IsBeingTargetedForGrapple(Pawn pawn)
        {
            if (pawn == null)
                return false;

            lock (pawnsBeingTargetedByGrapples)
            {
                return pawnsBeingTargetedByGrapples.Contains(pawn);
            }
        }

        public static bool TryRegisterGrabAttempt(Pawn pawn)
        {
            if (pawn == null)
                return false;

            lock (pawnsBeingTargetedByGrapples)
            {
                return pawnsBeingTargetedByGrapples.Add(pawn);
            }
        }

        public static bool EndGrabAttempt(Pawn pawn)
        {
            if (pawn == null)
                return false;

            lock (pawnsBeingTargetedByGrapples)
            {
                return pawnsBeingTargetedByGrapples.Remove(pawn);
            }
        }

        public static void Tick()
        {
            if (GenTicks.TicksGame % 480 == 0)
                pawnsBeingTargetedByGrapples.RemoveWhere(p => !p.Spawned || p.Dead);
        }

        public static IEnumerable<IntVec3> GetIdealGrappleSpots(Pawn grappler, Pawn target, bool onlyExecutionSpots)
        {
            // Assumes grappler and target are valid.

            var selfPos = grappler.Position;
            var targetPos = target.Position;
            var map = grappler.Map;
            var size = map.Size;

            if (onlyExecutionSpots)
            {
                bool toRight = selfPos.x <= targetPos.x;
                if (toRight)
                {
                    if (SpaceChecker.IsValidPawnPosFast(map, size.x, size.z, selfPos + new IntVec3(1, 0, 0)))
                        yield return selfPos + new IntVec3(1, 0, 0);
                    if (SpaceChecker.IsValidPawnPosFast(map, size.x, size.z, selfPos - new IntVec3(1, 0, 0)))
                        yield return selfPos - new IntVec3(1, 0, 0);
                }
                else
                {
                    if (SpaceChecker.IsValidPawnPosFast(map, size.x, size.z, selfPos - new IntVec3(1, 0, 0)))
                        yield return selfPos - new IntVec3(1, 0, 0);
                    if (SpaceChecker.IsValidPawnPosFast(map, size.x, size.z, selfPos + new IntVec3(1, 0, 0)))
                        yield return selfPos + new IntVec3(1, 0, 0);
                }
            }
            else
            {
                // TODO need a much better implementation of this.
                foreach (var p in GetFreeSpotsAround(grappler).OrderBy(pos => pos.DistanceToSquared(targetPos)))
                    yield return p;
            }
        }

        public static IEnumerable<IntVec3> GetFreeSpotsAround(Pawn pawn) => GetFreeSpotsAround(pawn, GenAdj.AdjacentCells);

        private static IEnumerable<IntVec3> GetFreeSpotsAround(Pawn pawn, IntVec3[] cells)
        {
            if (pawn == null)
                yield break;

            var basePos = pawn.Position;
            var map = pawn.Map;
            var size = map.Size;

            foreach (var offset in cells)
            {
                var pos = basePos + offset;
                if (pos.x < 0 || pos.z < 0 || pos.x >= size.x || pos.z >= size.z)
                    continue;

                if (SpaceChecker.IsValidPawnPosFast(map, size.x, size.z, pos))
                    yield return pos;
            }
        }

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
            trs = Matrix4x4.TRS(from + new Vector3(0, 0, -0.22f), Quaternion.identity, new Vector3(0.1f, 1f, 0.3f));
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

            var loaded = ContentFinder<Texture2D>.Get($"AM/BoundPawns/{bodyType.defName}");
            BoundPawnTextures.Add(bodyType, loaded);
            return loaded;
        }
    }
}
