using AM.Patches;
using FacialAnimation;
using HarmonyLib;

namespace AM.FacialAnimationPatch;

/// <summary>
/// DrawFaceGraphicsComp.CompRenderNodes draws all of the facial animation stuff, including a replacement head.
/// The pawns are considered invisible inside animations, and facial animation will use the 'invisible'
/// shader if it detects this.
/// This patch makes it so that during the facial animation rendering, the patch to make pawns be considered invisible is disabled.
/// </summary>
[HarmonyPatch(typeof(DrawFaceGraphicsComp), nameof(DrawFaceGraphicsComp.CompRenderNodes))]
public static class Patch_DrawFaceGraphicsComp_CompRenderNodes
{
    private static void Prefix(DrawFaceGraphicsComp __instance, ref bool __state)
    {
        __state = false;

        // Don't bother checking if invisible pawns are not enabled.
        if (!Core.Settings.AllowInvisiblePawns)
            return;

        var pawn = __instance.pawn;
        if (pawn == null)
            return;

        var animator = pawn.TryGetAnimator();
        if (animator != null)
        {
            __state = true;
            __instance.SetDirty();
            Patch_InvisibilityUtility_IsPsychologicallyInvisible.IsRendering = true;
        }
    }

    private static void Postfix(bool __state)
    {
        if (__state)
        {
            Patch_InvisibilityUtility_IsPsychologicallyInvisible.IsRendering = false;
        }
    }
}