using RimWorld;
using System.Collections.Generic;
using Verse;

namespace AM.Outcome;

public interface IOutcomeWorker
{
    IEnumerable<PossibleMeleeAttack> GetMeleeAttacksFor(ThingWithComps weapon, Pawn pawn);

    float GetChanceToPenAprox(Pawn pawn, BodyPartRecord bodyPart, StatDef armorType, float armorPen);

    float GetPen(ThingWithComps weapon, Verb verb, Pawn attacker);

    float GetDamage(ThingWithComps weapon, Verb verb, Pawn attacker);

    void PreDamage(Verb verb);
}
