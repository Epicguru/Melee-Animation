using UnityEngine;

namespace AAM.Events;

[CreateAssetMenu(fileName = "Mote", menuName = "Events/Mote")]
public class PuntPawnEvent : EventBase
{
    public override string EventID => "PuntPawn";

    public int PawnIndex;
    public bool Right = true;

    public override void Expose()
    {
        Look(ref PawnIndex);
        Look(ref Right);
    }
}
