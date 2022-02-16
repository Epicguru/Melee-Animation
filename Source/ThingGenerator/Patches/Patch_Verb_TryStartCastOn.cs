using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace AAM.Patches
{
    //typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(bool), typeof(bool), typeof(bool)
    [HarmonyPatch(typeof(Verb_MeleeAttack), "TryCastShot")]
    public static class Patch_Verb_TryStartCastOn
    {
        [TweakValue("__AAM", 0f, 1f)]
        public static float Chance = 0.1f;

        private static List<(AnimDef anim, bool mirrorX)> tempAnims = new List<(AnimDef anim, bool mirrorX)>(32);

        static void Postfix(Verb __instance, bool __result)
        {
            // Rand.Chance(Chance)
            if (__result && __instance.IsMeleeAttack && __instance.CasterIsPawn)
            {
                var pawn = __instance.CasterPawn;
                var weapon = __instance.EquipmentSource;
                var target = __instance.CurrentTarget.Pawn;
                if (target == null || weapon == null)
                    return;

                if (pawn.IsInAnimation() || target.IsInAnimation())
                    return;

                Core.Log($"{pawn.NameShortColored} hit {target} with a {weapon.LabelShortCap}.");
            }
        }
    }
}
