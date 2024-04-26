using HarmonyLib;
using JetBrains.Annotations;
using Verse;

namespace AM.Patches;

/// <summary>
/// Suppresses shadow draw done by PawnRenderer's DrawShadowInternal.
/// </summary>
[HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.DrawShadowInternal))]
[UsedImplicitly]
public class Patch_PawnRenderer_DrawShadowInternal
{
    public static bool Suppress = false;

    [HarmonyPriority(Priority.First)]
    public static bool Prefix() => !Suppress;
}