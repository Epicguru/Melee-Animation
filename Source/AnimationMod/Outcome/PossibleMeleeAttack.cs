using Verse;

namespace AM.Outcome;

public readonly struct PossibleMeleeAttack
{
    public Pawn Pawn { get; init; }
    public float Damage { get; init; }
    public float ArmorPen { get; init; }
    public DamageDef DamageDef { get; init; }
    public Verb Verb { get; init; }
    public ThingWithComps Weapon { get; init; }

    public override string ToString() => Verb.ToString();
}
