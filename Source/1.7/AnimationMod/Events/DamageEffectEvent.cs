using UnityEngine;

namespace AM.Events;

[CreateAssetMenu(fileName = "DamageEffect", menuName = "Events/DamageEffect")]
public class DamageEffectEvent : EventBase
{
    public override string EventID => "DamageEffect";

    public int PawnIndex;
    public Vector3 Offset;

    protected override void Expose()
    {
        Look(ref PawnIndex);
        Look(ref Offset);
    }
}