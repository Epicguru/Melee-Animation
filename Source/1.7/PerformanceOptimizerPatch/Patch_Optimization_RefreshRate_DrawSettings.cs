using HarmonyLib;
using JetBrains.Annotations;
using PerformanceOptimizer;
using Verse;

namespace AM.PerformanceOptimizerPatch;

/// <summary>
/// Prevents the optimization from apply patches, disabling it.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.Members)]
[HarmonyPatch(typeof(Optimization_RefreshRate), nameof(Optimization_RefreshRate.DrawSettings))]
public static class Patch_Optimization_RefreshRate_DrawSettings
{
    public static bool Prefix(Optimization __instance, Listing_Standard section)
    {
        if (__instance is not Optimization_PawnUtility_IsInvisible)
            return true;

        section.Label($"<color=red>{__instance.Label}</color> <i>{"AM.Patch.OptDisabled".Trs()}</i>");
        return false;
    }
}
