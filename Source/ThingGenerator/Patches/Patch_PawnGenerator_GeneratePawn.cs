using System;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Verse;

namespace AM.Patches;

/// <summary>
/// Used to spawn lassos on melee pawns when they first generate.
/// </summary>
[HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GenerateGearFor))]
[UsedImplicitly]
public static class Patch_PawnGenerator_GeneratePawn
{
    [HarmonyPriority(Priority.Last)]
    public static void Postfix(Pawn pawn)
    {
        try
        {
            if (pawn == null)
                return;

            // Spawn chance from settings:
            if (!Rand.Chance(Core.Settings.LassoSpawnChance))
                return;

            // Basic pawn checks.
            if (!pawn.def.race.Humanlike || !pawn.def.race.ToolUser || pawn.apparel == null)
                return;

            // Only give to pawns with melee weapons.
            var weapon = pawn.GetFirstMeleeWeapon();
            if (weapon == null)
                return;

            // Don't bother giving to pawns that do not have the required melee skill.
            if (Core.Settings.MinMeleeSkillToLasso > 0 && !HasSkillToUseLasso(pawn))
                return;

            GiveLasso(pawn, GetRandomLasso());
        }
        catch (Exception e)
        {
            Core.Error("Exception in pawn generation postfix:", e);
        }
    }

    private static bool HasSkillToUseLasso(Pawn pawn)
    {
        int skill = pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? -1;
        return skill >= Core.Settings.MinMeleeSkillToLasso;
    }

    private static void GiveLasso(Pawn pawn, ThingDef lasso)
    {
        if (pawn == null || lasso == null)
            return;

        var thing = ThingMaker.MakeThing(lasso) as Apparel;
        if (thing == null)
        {
            Core.Warn($"Failed to spawn instance of lass '{lasso}'");
            return;
        }

        thing.stackCount = 1;
        pawn.apparel.Wear(thing, false);
    }

    private static ThingDef GetRandomLasso()
    {
        // Random lasso with a heavy weight on cheaper ones.
        return Content.LassoDefs.RandomElementByWeightWithFallback(l => 1f / Mathf.Pow(l.BaseMarketValue, 3));
    }
}
