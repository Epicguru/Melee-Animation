using UnityEngine;

namespace AM.Sweep;

public interface ISweepProvider
{
    (Color low, Color high) GetTrailColors(in SweepProviderArgs args);
}

public struct SweepProviderArgs
{
    public float Time;
    public float LastTime;
    public float UpVel;
    public float DownVel;
    public AnimRenderer Renderer;
    public AnimPartData Part;
}
