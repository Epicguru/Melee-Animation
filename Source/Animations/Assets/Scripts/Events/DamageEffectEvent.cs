using UnityEngine;

[CreateAssetMenu(fileName = "DamageEffect", menuName = "Events/DamageEffect")]
public class DamageEffectEvent : EventBase
{
    public override string EventID => "DamageEffect";

    public int PawnIndex;
    public Vector3 Offset;

    public override void Expose()
    {
        Look(ref PawnIndex);
        Look(ref Offset);
    }
}
