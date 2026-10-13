using HarmonyLib;
using JetBrains.Annotations;
using SWSaber;

namespace AM.LightsaberPatch;

/// <summary>
/// Prevents the lightsaber mod from drawing the lightsaber blade,
/// this is already handled by the <see cref="SaberRenderer"/>.
/// </summary>
[HarmonyPatch(typeof(CompActivatableEffect.CompActivatableEffect), nameof(CompActivatableEffect.CompActivatableEffect.PostDraw))]
[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public sealed class Patch_CompActivatableEffect_PostDraw
{
    public static bool Prefix(CompActivatableEffect.CompActivatableEffect __instance)
    {
        return __instance is not CompLightsaberActivatableEffect;
    }
}
