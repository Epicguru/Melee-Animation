using System;
using AM.Idle;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace AM.Patches;

/// <summary>
/// A non-essential patch that makes it so that when apparel changes,
/// the idle animation updates its hands because the pawn may have equipped or un-equipped gloves.
/// This is a non-essential patch because the animation would have updated as soon as the animation
/// re-started, which happens quite frequently anyway.
/// </summary>
[UsedImplicitly]
[HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.Notify_ApparelChanged))]
public static class Patch_Pawn_ApparelTracker_Notify_ApparelChanged
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(Pawn ___pawn)
    {
        if (!Core.Settings.AnimateAtIdle)
            return;

        try
        {
            var idler = ___pawn.TryGetComp<IdleControllerComp>();
            idler?.CurrentAnimation?.ConfigureHandsForPawn(___pawn, 0);
        }
        catch (Exception e)
        {
            Core.Warn($"Clothes changed on {___pawn}, attempted to update idle animation hands but got exception:\n{e}");
        }
    }
}