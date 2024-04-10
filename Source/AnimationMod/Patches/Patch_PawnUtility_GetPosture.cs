using HarmonyLib;
using RimWorld;
using Verse;

namespace AM.Patches;

/// <summary>
/// Makes it so that pawns that are being animated stand up, ignoring regular posture calculation.
/// </summary>
[HarmonyPatch(typeof(PawnUtility), nameof(PawnUtility.GetPosture))]
public static class Patch_PawnUtility_GetPosture
{
    [HarmonyPriority(Priority.First)]
    public static bool Prefix(Pawn p, ref PawnPosture __result)
    {
        var anim = PatchMaster.GetAnimator(p);
        if (anim == null)
            return true;

        __result = PawnPosture.Standing;
        return false;
    }
}