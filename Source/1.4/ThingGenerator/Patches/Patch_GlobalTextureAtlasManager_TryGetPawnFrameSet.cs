using HarmonyLib;
using Verse;

namespace AM.Patches;

/// <summary>
/// Disables the texture caching introduced in Rimworld 1.3.
/// Only applies when a pawn in being animated, or when they have been beheaded.
/// </summary>
[HarmonyPatch(typeof(GlobalTextureAtlasManager), nameof(GlobalTextureAtlasManager.TryGetPawnFrameSet))]
public static class Patch_GlobalTextureAtlasManager_TryGetPawnFrameSet
{
    [HarmonyPriority(Priority.First)]
    public static bool Prefix(Pawn pawn, ref bool createdNew, ref bool __result)
    {
        var anim = PatchMaster.GetAnimator(pawn);
        if (anim == null)
        {
            var isBeheaded = AnimationManager.PawnToHeadInstance.TryGetValue(pawn, out _);
            if (!isBeheaded)
                return true;
        }

        createdNew = false;
        __result = false;
        return false;
    }
}