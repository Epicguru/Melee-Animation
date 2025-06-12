using HarmonyLib;
using RimWorld;

namespace AM.Patches;

/// <summary>
/// Used as part of the <see cref="Patch_Pawn_DrawTracker_Notify_MeleeAttackOn"/>
/// patch to do attack animations.
/// </summary>
[HarmonyPatch(typeof(Verb_MeleeAttack), nameof(Verb_MeleeAttack.TryCastShot))]
public static class Patch_Verb_MeleeAttack_TryCastShot
{
    public static Verb_MeleeAttack LatestInstance;

    [HarmonyPriority(Priority.First)]
    private static void Prefix(Verb_MeleeAttack __instance)
    {
        LatestInstance = __instance;
    }
}
