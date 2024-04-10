using HarmonyLib;
using JetBrains.Annotations;
using Verse;

namespace AM.Patches;

/// <summary>
/// Suppresses shadow draw which was added in 1.4 in the <see cref="PawnRenderer.RenderPawnAt(UnityEngine.Vector3, Rot4?, bool)"/>.
/// </summary>
[HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderShadowOnlyAt))]
[UsedImplicitly]
public class Patch_PawnRenderer_DrawInvisibleShadow
{
    public static bool Suppress = false;

    public static bool Prefix() => !Suppress;
}