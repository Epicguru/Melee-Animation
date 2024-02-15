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

            switch (Core.Settings.LassoSpawnChance)
            {
                case <= 0f:
                case < 1f when !Rand.Chance(Core.Settings.LassoSpawnChance):
                    return;
            }

            if (!pawn.def.race.Humanlike || !pawn.def.race.ToolUser || pawn.apparel == null)
                return;

            var weapon = pawn.GetFirstMeleeWeapon();
            if (weapon == null)
                return;

            GiveLasso(pawn, GetRandomLasso());
        }
        catch (Exception e)
        {
            Core.Error("Exception in pawn generation postfix:", e);
        }
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
        return Content.LassoDefs.RandomElementByWeightWithFallback(l => 1f / Mathf.Pow(l.BaseMarketValue, 4));
    }
}
