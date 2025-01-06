using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AM.Outcome;

public sealed class VanillaOutcomeWorker : IOutcomeWorker
{
    public IEnumerable<PossibleMeleeAttack> GetMeleeAttacksFor(ThingWithComps weapon, Pawn pawn)
    {
        // Hand fists:
        if (weapon == null)
        {
            foreach (var entry in pawn.meleeVerbs.GetUpdatedAvailableVerbsList(false))
            {
                var verb = entry.verb;
                if (!verb.IsMeleeAttack)
                    continue;

                float dmg = verb.verbProps.AdjustedMeleeDamageAmount(verb.tool, pawn, null, verb.HediffCompSource);
                float ap = verb.verbProps.AdjustedArmorPenetration(verb.tool, pawn, null, verb.HediffCompSource);
                yield return new PossibleMeleeAttack
                {
                    Damage = dmg,
                    ArmorPen = ap,
                    Pawn = pawn,
                    DamageDef = verb.GetDamageDef(),
                    Verb = verb,
                    Weapon = null
                };
            }
            yield break;
        }
        
        var comp = weapon?.GetComp<CompEquippable>();
        if (comp == null)
            yield break;

        foreach (var verb in comp.AllVerbs)
        {
            if (!verb.IsMeleeAttack)
                continue;

            float dmg = verb.verbProps.AdjustedMeleeDamageAmount(verb.tool, pawn, weapon, verb.HediffCompSource);
            float ap = verb.verbProps.AdjustedArmorPenetration(verb.tool, pawn, weapon, verb.HediffCompSource);
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

    public float GetChanceToPenAprox(Pawn pawn, BodyPartRecord bodyPart, StatDef armorType, float armorPen)
    {
        // Get skin & hediff chance-to-pen.
        float pawnArmor = pawn.GetStatValue(armorType) - armorPen;
        float chance = Mathf.Clamp01(1f - pawnArmor);

        if (pawn?.apparel == null)
            return chance;

        // Get apparel chance-to-pen.
        foreach (var a in pawn.apparel.WornApparel)
        {
            if (!a.def.apparel.CoversBodyPart(bodyPart))
                continue;

            var armor = a.GetStatValue(armorType);
            if (armor <= 0)
                continue;

            chance *= Mathf.Clamp01(1f - (armor - armorPen));
        }

        return chance;
    }

    public float GetPen(ThingWithComps weapon, Verb verb, Pawn attacker) =>
        verb.verbProps.AdjustedArmorPenetration(verb.tool, attacker, weapon, verb.HediffCompSource);

    public float GetDamage(ThingWithComps weapon, Verb verb, Pawn attacker) => 
        verb.verbProps.AdjustedMeleeDamageAmount(verb.tool, attacker, weapon, verb.HediffCompSource);

    // Does not need to do anything in vanilla.
    public void PreDamage(Verb verb) { }
}