using CompActivatableEffect;
using HarmonyLib;
using JetBrains.Annotations;
using SWSaber;
using Verse;

namespace AM.LightsaberPatch;

/// <summary>
/// Prevents the lightsaber mod from drawing the lightsaber blade,
/// this is already handled by the <see cref="SaberRenderer"/>.
/// </summary>
[HarmonyPatch(typeof(HarmonyCompActivatableEffect), nameof(HarmonyCompActivatableEffect.DrawEquipmentAimingPostFix))]
[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public class Patch_HarmonyCompActivatableEffect_DrawEquipmentAimingPostFix
{
    public static bool Prefix(Thing eq)
    {
        // Skip if the thing is a lightsaber.
        var comp = eq?.GetCompActivatableEffect() as CompLightsaberActivatableEffect;
        return comp == null;
    }
}