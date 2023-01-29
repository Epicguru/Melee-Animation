using UnityEngine;

namespace AAM.Events;

[CreateAssetMenu(fileName = "Clash", menuName = "Events/Clash")]
public class ClashAudioEvent : EventBase
{
    public override string EventID => "WeaponClash";

    public override void Expose()
    {

    }
}