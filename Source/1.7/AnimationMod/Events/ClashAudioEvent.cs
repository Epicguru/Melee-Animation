using UnityEngine;

namespace AM.Events;

[CreateAssetMenu(fileName = "Clash", menuName = "Events/Clash")]
public class ClashAudioEvent : EventBase
{
    public override string EventID => "WeaponClash";

    protected override void Expose()
    {

    }
}