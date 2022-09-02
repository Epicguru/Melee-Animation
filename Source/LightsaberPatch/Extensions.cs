using SWSaber;
using UnityEngine;
using Verse;

namespace AAM.LightsaberPatch;

public static class Extensions
{
    public static Color? TryGetLightsaberColor(this Thing lightsaber)
    {
        return lightsaber.TryGetComp<CompLightsaberActivatableEffect>()?.PostGraphicEffects(lightsaber.Graphic).Color;
    }
}