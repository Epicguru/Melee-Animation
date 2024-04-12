﻿using AM.Idle;
using AM.Tweaks;
using HarmonyLib;
using Verse;

namespace AM.Patches;

/// <summary>
/// Used to override drawing melee weapons.
/// </summary>
[HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAiming))]
public static class Patch_PawnRenderer_DrawEquipment
{
    [HarmonyPriority(Priority.First)]
    [HarmonyBefore("com.yayo.yayoAni")]
    private static bool Prefix(Thing eq)
    {
        if (!Core.Settings.AnimateAtIdle)
            return true;

        var pawn = eq.TryGetComp<CompEquippable>()?.Holder;
        if (pawn == null)
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
