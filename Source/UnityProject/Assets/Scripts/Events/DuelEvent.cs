using UnityEngine;

[CreateAssetMenu(fileName = "DuelEvent", menuName = "Events/DuelEvent")]
public class DuelEvent : EventBase
{
    public override string EventID => "DuelEvent";

    public override void Expose()
    {
    }
}