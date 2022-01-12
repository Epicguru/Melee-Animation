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

    [HarmonyPatch(typeof(PawnUtility), nameof(PawnUtility.GetPosture))]
    static class PosturePatch
    {
        [HarmonyPriority(Priority.First)]
        static bool Prefix(Pawn p, ref PawnPosture __result)
        {
            var anim = PatchMaster.GetAnimator(p);
            if (anim == null)
                return true;

            __result = PawnPosture.Standing;
            return false;
        }
    }

    [HarmonyPatch(typeof(GlobalTextureAtlasManager), "TryGetPawnFrameSet")]
    static class Patch_GlobalTextureAtlasManager_TryGetPawnFrameSet
    {
        [HarmonyPriority(Priority.First)]
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
    static class PreventDrawPatchUpper
    {
        public static bool AllowNext = false;

        [HarmonyPriority(Priority.First)]
        static bool Prefix(Pawn ___pawn)
        {
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
    }

    [HarmonyPatch(typeof(PawnRenderer), "RenderPawnInternal")]
    static class PreventDrawPatch
    {
        public static bool AllowNext = false;

        [HarmonyPriority(Priority.Last)] // As late as possible. We want to be the last to modify results.
        static bool Prefix(Pawn ___pawn, ref Rot4 bodyFacing, ref float angle)
        {
            var anim = PatchMaster.GetAnimator(___pawn);
            if (anim != null)
            {
                var body = anim.GetSnapshot(anim.GetPawnBody(___pawn));
                bool east = !body.FlipX;
                if (anim.MirrorHorizontal)
                    east = !east;

                angle = body.GetWorldRotation();

                bodyFacing = anim.Def.direction switch
                {
                    AnimDirection.Horizontal => east ? Rot4.East : Rot4.West,
                    AnimDirection.North => Rot4.North,
                    AnimDirection.South => Rot4.South,
                    _ => Rot4.East
                };
                if(!AllowNext)
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
        [HarmonyBefore("")]
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
