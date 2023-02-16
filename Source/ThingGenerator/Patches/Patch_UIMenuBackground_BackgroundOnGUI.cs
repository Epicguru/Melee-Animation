using AAM.UI;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AAM.Patches;

[HarmonyPatch(typeof(UI_BackgroundMain), nameof(UI_BackgroundMain.BackgroundOnGUI))]
public static class Patch_UIMenuBackground_BackgroundOnGUI
{
    private static void Postfix()
    {
        try
        {
            BGRenderer.DrawMainMenuBackground();
        }
        catch
        {
            // Nobody got time for all that noise.
        }
    }
}