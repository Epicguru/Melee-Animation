using HarmonyLib;
using RimWorld;
using Verse;

namespace AAM.Patches;

[HarmonyPatch(typeof(Verb_MeleeAttackDamage), nameof(Verb_MeleeAttackDamage.ApplyMeleeDamageToTarget))]
public static class Patch_Verb_MeleeAttackDamage_ApplyMeleeDamageToTarget
{
    public static DamageWorker.DamageResult lastResult;
    public static Thing lastTarget;

    public static void Postfix(LocalTargetInfo target, DamageWorker.DamageResult __result)
    {
        lastTarget = target.Thing;
        lastResult = __result;
    }
}
