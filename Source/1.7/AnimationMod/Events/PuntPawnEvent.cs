using UnityEngine;

namespace AM.Events;

[CreateAssetMenu(fileName = "Mote", menuName = "Events/Mote")]
public class PuntPawnEvent : EventBase
{
    public override string EventID => "PuntPawn";

    public int PawnIndex;
    public bool Right = true;

    protected override void Expose()
    {
        Look(ref PawnIndex);
        Look(ref Right);
    }
}
