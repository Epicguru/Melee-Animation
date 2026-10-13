using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;

namespace AM.Patches;

/// <summary>
/// Invisible pawns can't block pathing, but we do want pathing to be blocked
/// by pawns that are in animations.
/// This was a change introduced in Rimworld 1.5.
/// </summary>
[HarmonyPatch(typeof(PawnUtility), nameof(PawnUtility.PawnBlockingPathAt))]
[UsedImplicitly]
public static class Patch_PawnUtility_PawnBlockingPathAt
{
    [HarmonyPriority(Priority.First)]
    // ReSharper disable once RedundantAssignment
    private static void Prefix(ref bool __state)
    {
        __state = Patch_InvisibilityUtility_IsPsychologicallyInvisible.IsRendering;
        Patch_InvisibilityUtility_IsPsychologicallyInvisible.IsRendering = true;
    }

    [HarmonyPriority(Priority.First)]
    private static void Prefix(bool __state)
    {
        Patch_InvisibilityUtility_IsPsychologicallyInvisible.IsRendering = __state;
    }
}