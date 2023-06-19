using AM.UI;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AM.Patches;

[HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.AddDraftedOrders))]
[UsedImplicitly]
public class Patch_FloatMenuMakerMap_AddDraftedOrders
{
    [UsedImplicitly]
    private static void Postfix(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts)
    {
        opts.AddRange(DraftedFloatMenuOptionsUI.GenerateMenuOptions(clickPos, pawn));
    }
}