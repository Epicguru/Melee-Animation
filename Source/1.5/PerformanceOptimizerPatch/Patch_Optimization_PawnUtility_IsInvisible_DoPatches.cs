using HarmonyLib;
using JetBrains.Annotations;
using PerformanceOptimizer;

namespace AM.PerformanceOptimizerPatch;

/// <summary>
/// Prevents the optimization from apply patches, disabling it.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.Members)]
[HarmonyPatch(typeof(Optimization_PawnUtility_IsInvisible), nameof(Optimization_PawnUtility_IsInvisible.DoPatches))]
public static class Patch_Optimization_PawnUtility_IsInvisible_DoPatches
{
    [HarmonyPriority(Priority.First)]
    public static bool Prefix()
    {
        // Don't run original method, so doesn't patch anything.
        return false;
    }
}
