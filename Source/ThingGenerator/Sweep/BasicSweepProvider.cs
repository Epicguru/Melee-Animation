using UnityEngine;

namespace AAM.Sweep;

public class BasicSweepProvider : ISweepProvider
{
    public static readonly BasicSweepProvider DefaultInstance = new();

    public Color color = Color.white;
    public float length = 0.15f;
    public float minVel = 1f;
    public float maxVel = 2f;

    public (Color low, Color high) GetTrailColors(in SweepProviderArgs args)
    {
        float timeSinceHere = args.LastTime - args.Time;

        float l = length * Core.Settings.TrailLengthScale;
        Color col = color * Core.Settings.TrailColor;
        Color tint = args.Renderer.GetOverride(args.Part)?.TweakData?.TrailTint ?? Color.white;
        col *= tint;

        if (timeSinceHere > l)
            return (default, default);

        float sa = Mathf.InverseLerp(minVel, maxVel, args.DownVel);
        float sb = Mathf.InverseLerp(minVel, maxVel, args.UpVel);

        float a = Mathf.Clamp01(1f - timeSinceHere / l);
        var low = col;
        var high = col;
        low.a = a * sa;
        high.a = a * sb;

        return (low, high);
    }
}