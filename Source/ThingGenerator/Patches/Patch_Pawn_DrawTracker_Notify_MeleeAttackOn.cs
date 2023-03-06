using System;
using AM.Idle;
using HarmonyLib;
using Verse;

namespace AM.Patches;

[HarmonyPatch(typeof(Pawn_DrawTracker), nameof(Pawn_DrawTracker.Notify_MeleeAttackOn))]
public static class Patch_Pawn_DrawTracker_Notify_MeleeAttackOn
{
    public static void Prefix(Pawn_DrawTracker __instance, Thing Target)
    {
        if (!Core.Settings.AnimateAtIdle)
            return;

        try
        {
            var verbUsed = Patch_Verb_MeleeAttack_TryCastShot.LatestInstance;
            var comp = __instance.pawn.GetComp<IdleControllerComp>();
            comp?.NotifyPawnDidMeleeAttack(Target, verbUsed);
        }
        catch (Exception e)
        {
            Core.Error("Exception when notifying idle controller of melee attack", e);
        }
    }
}
