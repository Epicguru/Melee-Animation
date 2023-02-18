using AAM.Idle;
using HarmonyLib;
using Verse;

namespace AAM.Patches;

/// <summary>
/// A patch to control pawn animations.
/// </summary>
[HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.DrawEquipmentAiming))]
public static class Patch_PawnRenderer_DrawEquipmentAiming
{
    [HarmonyPriority(Priority.First + 100)]
    static bool Prefix(PawnRenderer __instance)
    {
        var pawn = __instance.pawn;

        if (!Core.Settings.AnimateAtIdle)
            return true;

        var comp = pawn.GetComp<IdleControllerComp>();
        if (comp == null)
            return true; // Why would this ever be the case? Better safe than sorry though.

        // Only for melee weapons...
        return pawn.GetFirstMeleeWeapon() == null;
    }
}
