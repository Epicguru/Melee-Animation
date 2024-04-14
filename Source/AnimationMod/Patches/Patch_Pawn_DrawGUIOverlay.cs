using System;
using HarmonyLib;
using JetBrains.Annotations;
using Verse;

namespace AM.Patches;

/// <summary>
/// Disables the default GUI (label) rendering of animated pawns.
/// Instead, the label is drawn externally. See <see cref="AnimRenderer.DrawSingle"/>
/// </summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.DrawGUIOverlay))]
[UsedImplicitly]
public static class Patch_Pawn_DrawGUIOverlay
{
    [HarmonyPriority(Priority.First)]
    public static bool Prefix(Pawn __instance)
    {
        var anim = PatchMaster.GetAnimator(__instance);
        if (anim != null)
            return false;

        return true;
    }
}