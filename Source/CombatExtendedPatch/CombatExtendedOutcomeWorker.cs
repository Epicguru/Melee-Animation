using AM.Outcome;
using CombatExtended;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace AM.CombatExtendedPatch;

public sealed class CombatExtendedOutcomeWorker : IOutcomeWorker
{
    public IEnumerable<PossibleMeleeAttack> GetMeleeAttacksFor(ThingWithComps weapon, Pawn pawn)
    {
        var comp = weapon?.GetComp<CompEquippable>();
        if (comp == null)
            yield break;

        foreach (var verb in comp.AllVerbs)
        {
            if (!verb.IsMeleeAttack)
                continue;

            float dmg = GetDamage(weapon, verb, pawn);
            float ap = GetPen(weapon, verb, pawn);

            yield return new PossibleMeleeAttack
            {
                Damage = dmg,
                ArmorPen = ap,
                Pawn = pawn,
                DamageDef = verb.GetDamageDef(),
                Verb = verb,
                Weapon = weapon
            };
        }
    }

    public float GetPen(ThingWithComps weapon, Verb verb, Pawn attacker)
    {
        var tool = verb.tool as ToolCE;
        if (tool == null)
            return verb.tool.armorPenetration * GetPenetrationFactor(weapon);

        var isBlunt = verb.GetDamageDef()?.armorCategory?.armorRatingStat == StatDefOf.ArmorRating_Blunt;
        if (isBlunt)
            return tool.armorPenetrationBlunt * GetPenetrationFactor(weapon);
        return tool.armorPenetrationSharp * GetPenetrationFactor(weapon);
    }

    public float GetDamage(ThingWithComps weapon, Verb verb, Pawn attacker)
        => verb.verbProps.AdjustedMeleeDamageAmount(verb.tool, attacker, weapon, verb.HediffCompSource);

    private float GetPenetrationFactor(Thing weapon)
        => weapon?.GetStatValue(CE_StatDefOf.MeleePenetrationFactor) ?? 1f;

    public float GetChanceToPenAprox(Pawn pawn, BodyPartRecord bodyPart, StatDef armorType, float armorPen)
    {
        // Get skin & hediff chance-to-pen.
        float armor = pawn.GetStatValue(armorType);

        if (pawn.apparel != null)
        {
            // Get apparel chance-to-pen.
            foreach (var a in pawn.apparel.WornApparel)
            {
                if (!a.def.apparel.CoversBodyPart(bodyPart))
                    continue;

                armor += a.GetStatValue(armorType);
            }
        }

        // 75% of the required pen gives 0% pen chance, increasing to 100% at 100% required pen.
        // Not perfect, but a reasonable approximation.
        float rawPct = armor <= 0f ? 1f : armorPen / armor;
        return OutcomeUtility.RemapClamped(0.75f, 1f, 0f, 1f, rawPct);
    }
}