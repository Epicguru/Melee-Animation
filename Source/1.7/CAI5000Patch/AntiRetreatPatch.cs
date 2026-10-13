using CombatAI;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace AM.CAI5000Patch;

/// <summary>
/// Stops the aggressive DuckOrRetreat job giver from interrupting animations.
/// </summary>
[HarmonyPatch(typeof(JobGiver_DuckOrRetreat), nameof(JobGiver_DuckOrRetreat.TryGiveJob))]
public static class AntiRetreatPatch
{
    public static bool Prefix(Pawn pawn, ref Job __result)
    {
        if (pawn != null && pawn.TryGetAnimator() == null)
            return true;

        __result = null;
        return false;
    }
}