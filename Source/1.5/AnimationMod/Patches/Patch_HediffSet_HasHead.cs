using HarmonyLib;
using Verse;

namespace AM.Patches;

[HarmonyPatch(typeof(HediffSet), nameof(HediffSet.HasHead), MethodType.Getter)]
public static class Patch_HediffSet_HasHead
{
    public static bool? ForcedHasHeadValue;

    [HarmonyPriority(Priority.First)]
    public static bool Prefix(ref bool __result)
    {
        if (ForcedHasHeadValue != null)
        {
            __result = ForcedHasHeadValue.Value;
            return false;
        }

        return true;
    }
}