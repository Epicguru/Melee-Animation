using UnityEngine;

namespace AAM.Events;

[CreateAssetMenu(fileName = "KillPawn", menuName = "Events/KillPawn")]
public class KillPawnEvent : EventBase
{
    public override string EventID => "KillPawn";

    [Header("Pawns")]
    public int KillerIndex;
    public int VictimIndex = 1;

    public string TargetBodyPart = "Heart";
    public string DamageDef = "Cut";
    public string BattleLogDef = "AAM_Execution_Generic";

    [Header("Misc")]
    public bool PreventDamageMote = true;

    public override void Expose()
    {
        Look(ref KillerIndex);
        Look(ref VictimIndex);
        Look(ref TargetBodyPart);
        Look(ref DamageDef);
        Look(ref BattleLogDef);
        Look(ref PreventDamageMote);
    }
}