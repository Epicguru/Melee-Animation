using AM.Grappling;
using HarmonyLib;
using Verse;

namespace AM.Patches;

/// <summary>
/// Simply prevents the regular RenderPawnAt method from running while a pawn in being animated.
/// This disables the regular rendering whenever a pawn in being animated.
/// Additionally, modifies PawnRenderer.results to make sure that the RenderPawnInternal method gets called every frame
/// while in an animation.
/// </summary>
[HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnAt))]
public static class Patch_PawnRenderer_RenderPawnAt
{
    public static bool AllowNext;

    private static PawnRenderer.PreRenderResults? storedResultsTemp;

    [HarmonyPriority(Priority.First)]
    public static bool Prefix(Pawn ___pawn, ref PawnRenderer.PreRenderResults ___results)
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

        if (AllowNext)
        {
            storedResultsTemp = ___results;
            ___results.showBody = true;
            ___results.useCached = false;
            ___results.valid = true;
        }

        AllowNext = false;
        return true;        
    }

    [HarmonyPriority(Priority.First)]
    public static void Postfix(ref PawnRenderer.PreRenderResults ___results)
    {
        if (storedResultsTemp != null)
        {
            ___results = storedResultsTemp.Value;
            storedResultsTemp = null;
        }
    }
}