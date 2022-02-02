using UnityEngine;

namespace AAM.Events;

[CreateAssetMenu(fileName = "Audio", menuName = "Events/Audio")]
public class AudioEvent : EventBase
{
    public override string EventID => "Audio";

    public string AudioDefName;
    public Vector3 LocalPosition;
    public float VolumeFactor = 1f;
    public float PitchFactor = 1f;
    public bool OnCamera = false;

    public override void Expose()
    {
        Look(ref AudioDefName);
        Look(ref LocalPosition);
        Look(ref VolumeFactor);
        Look(ref PitchFactor);
        Look(ref OnCamera);
    }
}