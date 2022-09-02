using AAM.Sweep;
using SWSaber;
using UnityEngine;
using Verse;

namespace AAM.LightsaberPatch;

public class LightsaberSweepProvider : ISweepProvider
{
    public Color color = Color.white;
    public const float length = 0.15f;
    public const float minVel = 1f;
    public const float maxVel = 2f;

    public LightsaberSweepProvider(Thing lightsaber)
    {
        var color = lightsaber.TryGetLightsaberColor();
        if (color == null)
        {
            Core.Error($"Tried to use {nameof(LightsaberSweepProvider)} with a Thing that does not have a {nameof(CompLightsaberActivatableEffect)}! ({lightsaber})");
            return;
        }

        this.color = color.Value;
    }

    public (Color low, Color high) GetTrailColors(in SweepProviderArgs args)
    {
        float timeSinceHere = args.LastTime - args.Time;
        if (timeSinceHere > length)
            return (default, default);

        float sa = Mathf.InverseLerp(minVel, maxVel, args.DownVel);
        float sb = Mathf.InverseLerp(minVel, maxVel, args.UpVel);

        float a = Mathf.Clamp01(1f - timeSinceHere / length);
        var low = color;
        var high = color;
        low.a = a * sa;
        high.a = a * sb;

        return (low, high);
    }
}