using System;
using AM.UI;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace AM.Patches;

public static class Patch_VBE_Utils_DrawBG
{
    private static UI_BackgroundMain BackgroundMain => UIMenuBackgroundManager.background as UI_BackgroundMain;

    public static void TryApplyPatch()
    {
        try
        {
            var orig = AccessTools.Method("VBE.BackgroundController:DoOverlay");
            var orig2 = AccessTools.Method("VBE.Utils:DrawBG");
            var replacement = AccessTools.Method(typeof(Patch_VBE_Utils_DrawBG), nameof(DoOverlayPrefix));
            var replacement2 = AccessTools.Method(typeof(Patch_VBE_Utils_DrawBG), nameof(DrawBGPrefix));

            if (orig == null)
                throw new Exception("Failed to find original method 1.");
            if (orig2 == null)
                throw new Exception("Failed to find original method 2.");
            if (replacement == null)
                throw new Exception("Failed to find local prefix method.");

            Core.Harmony.Patch(orig, prefix: new HarmonyMethod(replacement, Priority.First));
            Core.Harmony.Patch(orig2, prefix: new HarmonyMethod(replacement2, Priority.First));
            Core.Log("Successfully patched VBE with custom background renderer.");
        }
        catch (Exception e)
        {
            Core.Error("Failed to apply VBE patch, VBE has probably changed/updated:", e);
        }
    }

    private static bool ShouldDraw()
    {
        // Draw when the image is the target.
        // Also have to draw when bg has been overriden, this is just the way that VBE works.
        //Core.Log($"bg: {BackgroundMain?.overrideBGImage}");
        return BackgroundMain?.overrideBGImage == Content.BGSketch1;
    }

    public static bool DoOverlayPrefix()
    {
        if (ShouldDraw())
            DrawCustom();

        return true;
    }

    public static bool DrawBGPrefix(Texture texture)
    {
        return texture != Content.BGSketch1 || !ShouldDraw();
    }

    private static void DrawCustom()
    {
        BGRenderer.DrawMainMenuBackground();
    }
}