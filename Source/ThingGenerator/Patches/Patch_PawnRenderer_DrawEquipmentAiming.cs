using AM.Idle;
using AM.Tweaks;
using HarmonyLib;
using Verse;

namespace AM.Patches;

/// <summary>
/// Used to override drawing melee weapons.
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
        var wep = pawn.equipment?.Primary?.def;
        bool isMeleeWeapon = wep?.IsMeleeWeapon() ?? false;
        if (isMeleeWeapon && TweakDataManager.TryGetTweak(wep) == null)
            isMeleeWeapon = false;
        comp.PreDraw();
        return !isMeleeWeapon;
    }
}
