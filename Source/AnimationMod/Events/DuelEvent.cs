using UnityEngine;

namespace AM.Events;

[CreateAssetMenu(fileName = "DuelEvent", menuName = "Events/DuelEvent")]
public class DuelEvent : EventBase
{
    public override string EventID => "DuelEvent";

    protected override void Expose()
    {
    }
}