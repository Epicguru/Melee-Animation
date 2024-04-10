using AM.Grappling;
using HarmonyLib;
using Verse;

namespace AM.Patches;

/// <summary>
/// Simply prevents the regular RenderPawnAt method from running while a pawn in being animated.
/// This disables the regular rendering whenever a pawn in being animated.
/// </summary>
[HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnAt))]
public static class Patch_PawnRenderer_RenderPawnAt
{
    public static bool AllowNext;

    [HarmonyPriority(Priority.First)]
    public static bool Prefix(Pawn ___pawn)
    {
        var anim = PatchMaster.GetAnimator(___pawn);

        if (anim != null && !AllowNext)
        {
            return false;
        }

        var job = ___pawn.CurJob;
        if (job?.def == AM_DefOf.AM_GrapplePawn)
        {
            JobDriver_GrapplePawn.DrawEnsnaringRope(___pawn, job);
        }

        AllowNext = false;
        return true;        
    }
}