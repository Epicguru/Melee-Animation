using RimWorld;
using System.Collections.Generic;
using Verse;

namespace AM.Outcome;

public interface IOutcomeWorker
{
    IEnumerable<PossibleMeleeAttack> GetMeleeAttacksFor(ThingWithComps weapon, Pawn pawn);

    float GetChanceToPenAprox(Pawn pawn, BodyPartRecord bodyPart, StatDef armorType, float armorPen);
}
