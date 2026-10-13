using HarmonyLib;
using Verse;

namespace AM.Patches;

/// <summary>
/// This patch is only used to prevent pawns from rendering as invisible when they are in the portrait.
/// </summary>
[HarmonyPatch(typeof(PawnRenderTree), nameof(PawnRenderTree.ParallelPreDraw))]
public class Patch_PawnRenderTree_ParallelPreDraw
{
    [HarmonyPriority(Priority.First)]
    public static void Prefix(PawnDrawParms parms, ref State __state)
    {
        if (!parms.flags.HasFlag(PawnRenderFlags.Portrait))
        {
            __state = default;
            return;
        }

        if (!Patch_InvisibilityUtility_IsPsychologicallyInvisible.IsRendering)
        {
            __state.DidChangeIsRenderingToTrue = true;
            Patch_InvisibilityUtility_IsPsychologicallyInvisible.IsRendering = true;
        }
    }

    [HarmonyPriority(Priority.First)]
    public static void Postfix(ref State __state)
    {
        if (__state.DidChangeIsRenderingToTrue)
        {
            Patch_InvisibilityUtility_IsPsychologicallyInvisible.IsRendering = false;
        }
    }

    public struct State
    {
        public bool DidChangeIsRenderingToTrue;
    }
}