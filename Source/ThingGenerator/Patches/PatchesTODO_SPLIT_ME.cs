using HarmonyLib;
using RimWorld;
using Verse;

namespace AAM.Patches
{
    public static class PatchMaster
    {
        private static Pawn lastPawn;
        private static AnimRenderer lastRenderer;

        public static AnimRenderer GetAnimator(Pawn pawn)
        {
            if (pawn == lastPawn && lastRenderer != null && !lastRenderer.IsDestroyed)
                return lastRenderer;

            lastPawn = pawn;
            lastRenderer = AnimRenderer.TryGetAnimator(pawn);
            return lastRenderer;
        }
    }

    /// <summary>
    /// Makes it so that pawns that are being animated stand up, ignoring regular posture calculation.
    /// </summary>
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

    /// <summary>
    /// Disables the texture caching introduced in Rimworld 1.3.
    /// Only applies when a pawn in being animated.
    /// </summary>
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

    /// <summary>
    /// Simply prevents the regular RenderPawnAt method from running while a pawn in being animated.
    /// This disables the regular rendering whenever a pawn in being animated.
    /// </summary>
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

    /// <summary>
    /// Overrides various parameters of the pawn rendering, specifically direction (north, east etc.)
    /// and body angle. Driven by the active animation.
    /// </summary>
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

                bool north = anim.Def.direction != AnimDirection.South;
                if (anim.MirrorVertical)
                    north = !north;
                if (body.FlipY)
                    north = !north;

                angle = body.GetWorldRotation();

                // TODO replace body facing with animation driven parameter.
                bodyFacing = anim.Def.direction switch
                {
                    AnimDirection.Horizontal => east ? Rot4.East : Rot4.West,
                    AnimDirection.North or AnimDirection.South => north ? Rot4.North : Rot4.South,
                    _ => Rot4.East
                };
                if(!AllowNext)
                    return false;
            }

            AllowNext = false;
            return true;
        }
    }

    /// <summary>
    /// Disables the default GUI (label) rendering of animated pawns.
    /// Instead, the label is drawn externally. See <see cref="AnimRenderer.DrawSingle(AnimRenderer, float, System.Action{Pawn, UnityEngine.Vector2})"/>
    /// </summary>
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

    /// <summary>
    /// Make pawns be considered invisible during animations.
    /// This should prevent them from being targeted by enemies.
    /// This is a common feature in games where executions are possible - the player is invincible during the execution animation.
    /// For example, see Doom 2016 or any of the FromSoftware souls-likes.
    /// However, making pawns invincible during animations would be very overpowered and broken, so making them untargettable instead is a nice
    /// compromise.
    /// </summary>
    [HarmonyPatch(typeof(PawnUtility), "IsInvisible")]
    static class MakePawnConsideredInvisible
    {
        public static bool IsRendering;

        [HarmonyPriority(Priority.First)]
        static bool Prefix(Pawn pawn, ref bool __result)
        {
            if (!Core.Settings.AllowInvisiblePawns)
                return true;

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
