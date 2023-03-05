using AM.Idle;
using HarmonyLib;
using Verse;

namespace AM.Patches;

/// <summary>
/// A patch to control pawn animations.
/// </summary>
[HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.DrawEquipment))]
public static class Patch_PawnRenderer_DrawEquipment
{
    [HarmonyPriority(Priority.First)]
    [HarmonyBefore("com.yayo.yayoAni")]
    static bool Prefix(PawnRenderer __instance)
    {
        var pawn = __instance.pawn;

        if (!Core.Settings.AnimateAtIdle)
            return true;

        var comp = pawn.GetComp<IdleControllerComp>();
        if (comp == null)
            return true; // Why would this ever be the case? Better safe than sorry though.

        // Only for melee weapons...
        bool isMeleeWeapon = pawn.equipment?.Primary?.def.IsMeleeWeapon ?? false;
        comp.PreDraw();
        return !isMeleeWeapon;
    }
}
