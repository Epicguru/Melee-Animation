using System.Collections.Generic;
using AM.UI;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Verse;

namespace AM.Patches;

[HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.GetOptions))]
[UsedImplicitly]
public class Patch_FloatMenuMakerMap_GetOptions
{
    [UsedImplicitly]
    private static void Postfix(Vector3 clickPos, List<Pawn> selectedPawns, List<FloatMenuOption> __result)
    {
        if (selectedPawns.Count != 1)
            return;
        
        __result.AddRange(DraftedFloatMenuOptionsUI.GenerateMenuOptions(clickPos, selectedPawns[0]));
    }
}