﻿using HarmonyLib;
using RimWorld;
using Verse;

namespace AM.Patches;

/// <summary>
/// Make pawns be considered invisible during animations.
/// This should prevent them from being targeted by enemies.
/// This is a common feature in games where executions are possible - the player is invincible during the execution animation.
/// For example, see Doom 2016 or any of the FromSoftware souls-likes.
/// However, making pawns invincible during animations would be very overpowered and broken, so making them untargettable instead is a nice
/// compromise.
/// </summary>
[HarmonyPatch(typeof(PawnUtility), nameof(PawnUtility.IsInvisible))]
public static class Patch_PawnUtility_IsInvisible
{
    public static bool IsRendering;

    [HarmonyPriority(Priority.First)]
    public static bool Prefix(Pawn pawn, ref bool __result)
    {
        if (!Core.Settings.AllowInvisiblePawns)
            return true;

        var anim = PatchMaster.GetAnimator(pawn);
        if (anim != null && !IsRendering)
        {
            __result = true;
            return false;
        }
        return true;
    }
}