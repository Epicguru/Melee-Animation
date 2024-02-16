using HarmonyLib;
using UnityEngine;
using Verse;

namespace AM.Patches;

/// <summary>
/// Overrides various parameters of the pawn rendering, specifically direction (north, east etc.)
/// and body angle. Driven by the active animation.
/// </summary>
[HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnInternal))]
static class Patch_PawnRenderer_RenderPawnInternal
{
    public static bool AllowNext;
    public static bool DoNotModify = false;
    public static DrawMode NextDrawMode = DrawMode.Full;
    public static Rot4 HeadRotation; // Only used when NextDrawMode is HeadOnly or HeadStandalone.

    public static float StandaloneHeadRotation;

    public enum DrawMode
    {
        Full,
        BodyOnly,
        HeadOnly,
        HeadStandalone
    }

    [HarmonyPriority(Priority.Last)] // As late as possible. We want to be the last to modify results.
    [HarmonyAfter("com.yayo.yayoAni")] // Go away.
    [HarmonyBefore("rimworld.Nals.FacialAnimation")] // Must go before facial animation otherwise the face gets fucky.
    public static bool Prefix(Pawn ___pawn, ref Rot4 bodyFacing, ref float angle, ref PawnRenderFlags flags, ref bool renderBody)
    {
        // Do not affect portrait rendering:
        if (flags.HasFlag(PawnRenderFlags.Portrait))
            return true;

        // Standalone head (i.e. dropped head on ground after animation) gets a custom method that does things slightly differently.
        if (NextDrawMode == DrawMode.HeadStandalone)
            return RenderStandaloneHeadMode(ref bodyFacing, ref flags, ref angle, ref renderBody);

        // Get the animator for this pawn.
        var anim = PatchMaster.GetAnimator(___pawn);
        if (anim != null)
        {
            if (!DoNotModify)
            {
                var part = NextDrawMode == DrawMode.HeadOnly ? anim.GetPawnHead(___pawn) : anim.GetPawnBody(___pawn);
                var snapshot = anim.GetSnapshot(part);
                angle = snapshot.GetWorldRotation();

                bodyFacing = NextDrawMode == DrawMode.HeadOnly ? HeadRotation : snapshot.GetWorldDirection();

                switch (NextDrawMode)
                {
                    case DrawMode.BodyOnly:
                        // Render head stump, do not render head gear.
                        flags |= PawnRenderFlags.HeadStump;
                        flags &= ~PawnRenderFlags.Headgear;
                        break;

                    case DrawMode.HeadOnly:
                        // Do not render body.
                        renderBody = false;
                        break;
                }
            }

            if (!AllowNext)
                return false;
        }

        AllowNext = false;
        return true;
    }

    private static bool RenderStandaloneHeadMode(ref Rot4 bodyFacing, ref PawnRenderFlags flags, ref float angle, ref bool renderBody)
    {
        // Add headgear, remove head stump.
        flags |= PawnRenderFlags.Headgear;
        flags &= ~PawnRenderFlags.HeadStump;

        angle = StandaloneHeadRotation;
        bodyFacing = HeadRotation;
        renderBody = false;
        return true;
    }
}