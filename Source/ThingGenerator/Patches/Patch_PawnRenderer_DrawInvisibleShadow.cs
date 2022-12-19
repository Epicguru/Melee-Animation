using HarmonyLib;
using Verse;

namespace AAM.Patches;

/// <summary>
/// Suppresses shadow draw which was added in 1.4 in the <see cref="PawnRenderer.PawnRenderer(Pawn)"/>.
/// </summary>
[HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.DrawInvisibleShadow))]
public class Patch_PawnRenderer_DrawInvisibleShadow
{
    public static bool Suppress = false;

    public static bool Prefix() => !Suppress;
}