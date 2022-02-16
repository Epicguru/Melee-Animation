using UnityEngine;

namespace AAM.Events;

[CreateAssetMenu(fileName = "CamShake", menuName = "Events/CamShake")]
public class CamShakeEvent : EventBase
{
    // TODO IMPLEMENT AND FINISH.

    public override string EventID => "CamShakeEvent";

    public float Magnitude = 3;
    public float MaxDistance = 20;
    public float MaxZoom = 5;

    public override void Expose()
    {
        Look(ref Magnitude, 3);
        Look(ref MaxDistance, 20);
        Look(ref MaxZoom, 5);
    }
}