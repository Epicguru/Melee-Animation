using UnityEngine;

namespace AM.Events;

[CreateAssetMenu(fileName = "Mote", menuName = "Events/Mote")]
public class MoteEvent : EventBase
{
    public override string EventID => "Mote";

    public string MoteDef = "DustPuffThick";

    [Header("Initial position")]
    public string PartName;
    public Vector3 WithOffset;

    [Header("Appearance")]
    public Color CustomColor = default;

    [Header("velocity")]
    public Vector2 StartRotationSpeed;
    public Vector2 StartVelocityMagnitude = new(1, 2);
    public Vector2 StartVelocityAngle;
    public Vector2 StartScale = Vector2.one;

    protected override void Expose()
    {
        Look(ref MoteDef);
        Look(ref PartName);
        Look(ref WithOffset);
        Look(ref CustomColor);
        Look(ref StartRotationSpeed);
        Look(ref StartVelocityMagnitude);
        Look(ref StartVelocityAngle);
        Look(ref StartScale);
    }
}