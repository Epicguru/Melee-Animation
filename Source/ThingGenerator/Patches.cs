using HarmonyLib;
using RimWorld;
using Verse;

namespace AAM
{
    public static class PatchMaster
    {
        private static Pawn lastPawn;
        private static AnimRenderer lastRenderer;

        public static AnimRenderer GetAnimator(Pawn pawn)
        {
            if (pawn == lastPawn && lastRenderer != null && !lastRenderer.Destroyed)
                return lastRenderer;

            lastPawn = pawn;
            lastRenderer = AnimRenderer.TryGetAnimator(pawn);
            return lastRenderer;
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.BodyAngle))]
    static class AnglePatch
    {
        static bool Prefix(Pawn ___pawn, ref float __result)
        {
            var anim = PatchMaster.GetAnimator(___pawn);
            if (anim == null)
                return true;

            __result = anim.GetPawnBody(___pawn).CurrentSnapshot.GetWorldRotation(anim.RootTransform);
            return false;
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.LayingFacing))]
    static class FacingPatch
    {
        static bool Prefix(Pawn ___pawn, ref Rot4 __result)
        {
            var anim = PatchMaster.GetAnimator(___pawn);
            if (anim == null)
                return true;

            bool west = anim.GetPawnBody(___pawn).CurrentSnapshot.FlipX;
            if (anim.MirrorHorizontal)
                west = !west;

            __result = west ? Rot4.West : Rot4.East;
            return false;
        }
    }

    [HarmonyPatch(typeof(PawnUtility), nameof(PawnUtility.GetPosture))]
    static class PosturePatch
    {
        static bool Prefix(Pawn p, ref PawnPosture __result)
        {
            var anim = PatchMaster.GetAnimator(p);
            if (anim == null)
                return true;

            __result = PawnPosture.LayingOnGroundNormal;
            return false;
        }
    }

    [HarmonyPatch(typeof(GlobalTextureAtlasManager), "TryGetPawnFrameSet")]
    static class Patch_GlobalTextureAtlasManager_TryGetPawnFrameSet
    {
        static bool Prefix(Pawn pawn, ref bool createdNew, ref bool __result)
        {
            var anim = PatchMaster.GetAnimator(pawn);
            if (anim == null)
                return true;

            createdNew = false;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "RenderPawnAt")]
    static class PreventDrawPatch
    {
        public static bool AllowNext = false;

        [HarmonyPriority(Priority.First)]
        static bool Prefix(Pawn ___pawn)
        {
            var anim = PatchMaster.GetAnimator(___pawn);
            if (anim != null && !AllowNext)
            {
                return false;
            }

            AllowNext = false;
            return true;
        }
    }

    [HarmonyPatch(typeof(Pawn), "DrawGUIOverlay")]
    static class PreventGUIPatch
    {
        [HarmonyPriority(Priority.First)]
        static bool Prefix(Pawn __instance)
        {
            var anim = PatchMaster.GetAnimator(__instance);
            if (anim != null)
                return false;

            return true;
        }
    }

    [HarmonyPatch(typeof(PawnUtility), "IsInvisible")]
    static class MakePawnConsideredInvisible
    {
        public static bool IsRendering;

        [HarmonyPriority(Priority.First)]
        static bool Prefix(Pawn pawn, ref bool __result)
        {
            var anim = PatchMaster.GetAnimator(pawn);
            if (anim != null && !IsRendering)
            {
                __result = true;
                return false;
            }
            return true;
        }
    }
}
