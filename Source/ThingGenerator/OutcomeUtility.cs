using AAM.Patches;
using MathNet.Numerics.Distributions;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace AAM;

/// <summary>
/// Helper class to determine and then execute the outcome of duels and executions.
/// </summary>
public static class OutcomeUtility
{
    public ref struct AdditionalArgs
    {
        public DamageDef DamageDef;
        public BodyPartDef BodyPartDef;
        public RulePackDef LogGenDef;
        public Thing Weapon;
        public float TargetDamageAmount;
    }

    [DebugAction("Advanced Animation Mod", "Compare Lethality", allowedGameStates = AllowedGameStates.PlayingOnMap, actionType = DebugActionType.ToolMapForPawns)]
    private static void CompareLethalityDebug(Pawn pawn)
    {
        Pawn selected = Find.Selector.SelectedPawns.FirstOrDefault();
        if (selected == null || selected == pawn)
        {
            Messages.Message("Select another pawn before using the debug tool.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        Core.Log($"{selected}, {pawn}");

        float a  = selected.GetStatValue(AAM_DefOf.AAM_Lethality);
        float a2 = selected.GetStatValue(AAM_DefOf.AAM_DuelAbility);
        float b  = pawn.GetStatValue(AAM_DefOf.AAM_Lethality);
        float b2 = pawn.GetStatValue(AAM_DefOf.AAM_DuelAbility);
        float chanceToBeat = ChanceToBeatInMelee(selected, pawn);

        Messages.Message($"{selected.LabelShortCap} lethality: {a:P1}", MessageTypeDefOf.NeutralEvent, false);
        Messages.Message($"{selected.LabelShortCap} duel ability: {a2:P1}", MessageTypeDefOf.NeutralEvent, false);
        Messages.Message($"{pawn.LabelShortCap} lethality: {b:P1}", MessageTypeDefOf.NeutralEvent, false);
        Messages.Message($"{pawn.LabelShortCap} duel ability: {b2:P1}", MessageTypeDefOf.NeutralEvent, false);
        Messages.Message($"{selected.LabelShortCap} vs {pawn.LabelShortCap}: {chanceToBeat:P1} to win.", MessageTypeDefOf.NeutralEvent, false);

    }

    /// <summary>
    /// Performs the effects of <paramref name="outcome"/> on the target pawn.
    /// </summary>
    public static bool PerformOutcome(ExecutionOutcome outcome, Pawn attacker, Pawn target, in AdditionalArgs args)
    {
        if (target == null)
            return false;
        if (attacker == null)
            return false;
        if (target.Dead || !target.Spawned)
            return false;

        switch (outcome)
        {
            case ExecutionOutcome.Nothing:
                return true;

            case ExecutionOutcome.Damage:
                return Damage(attacker, target, args);

            case ExecutionOutcome.Down:
                return Down(attacker, target, args);

            case ExecutionOutcome.Kill:
                return Kill(attacker, target, args);

            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null);
        }
    }

    /// <summary>
    /// The percentage chance that <paramref name="a"/> has to beat <paramref name="b"/> in a duel or execution, based on the
    /// Duel Ability stat.
    /// </summary>
    public static float ChanceToBeatInMelee(Pawn a, Pawn b)
    {
        var normal = Core.Settings.GetNormalDistribution();
        float aL = a.GetStatValue(AAM_DefOf.AAM_DuelAbility);
        float bL = b.GetStatValue(AAM_DefOf.AAM_DuelAbility);
        float diff = aL - bL;

        return Mathf.Clamp01((float)normal.CumulativeDistribution(diff));
    }

    public static ExecutionOutcome GenerateRandomOutcome(Pawn attacker, Pawn victim) => GenerateRandomOutcome(attacker, victim, out _);

    public static ExecutionOutcome GenerateRandomOutcome(Pawn attacker, Pawn victim, out float pct)
    {
        Debug.Assert(attacker != null);
        Debug.Assert(victim != null);

        // Get lethality, adjusted by settings.
        float aL = attacker.GetStatValue(AAM_DefOf.AAM_Lethality);

        // Get a random outcome.
        var outcome = GenerateRandomOutcome(aL, out pct);

        // If the victim is friendly, do not allow them to be killed.
        if (Core.Settings.ExecutionsOnFriendliesAreNotLethal && outcome == ExecutionOutcome.Kill && (victim.IsColonist || victim.IsPrisonerOfColony))
            outcome = ExecutionOutcome.Down;

        return outcome;
    }

    /// <summary>
    /// Generates a random execution outcome based on an lethality value.
    /// </summary>
    public static ExecutionOutcome GenerateRandomOutcome(float lethality, out float pct)
    {
        if (lethality <= 0f)
        {
            pct = 1f;
            return ExecutionOutcome.Damage;
        }

        if (Rand.Chance(lethality))
        {
            pct = lethality;
            return ExecutionOutcome.Kill;
        }

        pct = (1f - lethality) * 0.5f;
        return Rand.Chance(0.5f) ? ExecutionOutcome.Down : ExecutionOutcome.Damage;
    }

    private static bool Damage(Pawn attacker, Pawn pawn, in AdditionalArgs args)
    {
        // Damage but do not kill or down the target.
        // Adapted from HealthUtility.GiveRandomSurgeryInjuries

        const float MIN_HP = 2;
        const float MAX_SINGLE_DAMAGE = 10;
        const float PART_PCT_MIN = 0.6f;
        const float PART_PCT_MAX = 0.6f;

        if (args.TargetDamageAmount <= 0f)
        {
            Core.Error("When calling Damage there should be TargetDamageAmount specified in the args.");
            return false;
        }

        // Hit external parts.
        IEnumerable<BodyPartRecord> partsToHit = 
            from x in pawn.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Outside, null, null)
            where !x.def.conceptual
            select x;

        // Don't hit parts that are too squishy.
        partsToHit =
            from x in partsToHit
            where HealthUtility.GetMinHealthOfPartsWeWantToAvoidDestroying(x, pawn) >= MIN_HP
            select x;

        float damageToDo = args.TargetDamageAmount;
        int missCount = 0;
        int i = 0;

        while (damageToDo > 0 && partsToHit.Any())
        {
            i++;
            if (i > 500)
            {
                Core.Error("Stopped infinite loop. Investigate bug.");
                break;
            }

            BodyPartRecord partToHit = partsToHit.RandomElementByWeight(x => x.coverageAbs);
            float partHealth = pawn.health.hediffSet.GetPartHealth(partToHit);

            float damage = Mathf.Max(MAX_SINGLE_DAMAGE, GenMath.RoundRandom(partHealth * Rand.Range(PART_PCT_MIN, PART_PCT_MAX)));
            float minHealthOfPartsWeWantToAvoidDestroying = HealthUtility.GetMinHealthOfPartsWeWantToAvoidDestroying(partToHit, pawn);

            // Do not leave part at less than 1 hp.
            if (minHealthOfPartsWeWantToAvoidDestroying - damage < MIN_HP)
                damage = Mathf.RoundToInt(minHealthOfPartsWeWantToAvoidDestroying - MIN_HP);

            // Pick damage type - use blunt or crush to avoid bleeding if necessary.
            DamageDef damageDef = args.DamageDef;
            if (args.DamageDef == null || partToHit.def.bleedRate > 0)
                damageDef = Rand.Element(DamageDefOf.Crush, DamageDefOf.Blunt);

            while (damage > 0)
            {
                // Reduce damage by 1 until it will no longer cause any serious damage.
                if (WouldBeDownDeadOrAmputated(damageDef, pawn, partToHit, damage))
                    damage -= 1;
                else
                    break;
            }

            if (damage <= 0)
            {
                // A miss is when damage could not be applied without causing serious damage.
                // After a couple of attempts, give up.
                missCount++;
                if (missCount > 2)
                    break;
            }

            DamageInfo dInfo = new DamageInfo(damageDef, damage, 0f, -1f, attacker, partToHit, args.Weapon?.def);
            dInfo.SetIgnoreArmor(true);
            dInfo.SetIgnoreInstantKillProtection(false);

            pawn.TakeDamage(dInfo);
            damageToDo -= damage;
        }

        return damageToDo <= 0;
    }

    private static bool WouldBeDownDeadOrAmputated(DamageDef damageDef, Pawn pawn, BodyPartRecord bodyPart, float damage)
    {
        HediffDef hediff = HealthUtility.GetHediffDefFromDamage(damageDef, pawn, bodyPart);

        return pawn.health.WouldBeDownedAfterAddingHediff(hediff, bodyPart, damage) ||
               pawn.health.WouldDieAfterAddingHediff(hediff, bodyPart, damage) ||
               pawn.health.WouldLosePartAfterAddingHediff(hediff, bodyPart, damage);
    }

    private static bool Down(Pawn attacker, Pawn pawn, in AdditionalArgs args)
    {
        // Smack them around until they go down.
        // Avoid bleeding.

        // Check if already downed...
        if (pawn.Downed)
            return true;

        // TODO this ignores args.DamageDef.
        HealthUtility.DamageUntilDowned(pawn, false);
        return true;
    }

    private static bool Kill(Pawn killer, Pawn pawn, in AdditionalArgs args)
    {
        if (Core.Settings.ExecutionsCanDestroyBodyParts)
        {
            BodyPartDef partDef = args.BodyPartDef;
            DamageDef dmgDef = args.DamageDef ?? DamageDefOf.Cut;
            RulePackDef logDef = args.LogGenDef ?? AAM_DefOf.AAM_Execution_Generic;
            BodyPartRecord part = pawn.TryGetPartFromDef(partDef);
            Thing weapon = args.Weapon;

            var dInfo = new DamageInfo(dmgDef, 99999, 99999, hitPart: part, instigator: killer, weapon: weapon?.def);
            var log = CreateLog(logDef, killer.equipment?.Primary, killer, pawn);
            dInfo.SetAllowDamagePropagation(false);
            dInfo.SetIgnoreArmor(true);
            dInfo.SetIgnoreInstantKillProtection(true);

            var oldEffecter = pawn.RaceProps?.FleshType?.damageEffecter;
            if (oldEffecter != null)
                pawn.RaceProps.FleshType.damageEffecter = null;

            // Smack em hard.
            DamageWorker.DamageResult result;
            try
            {
                result = pawn.TakeDamage(dInfo);
            }
            finally
            {
                if (oldEffecter != null)
                    pawn.RaceProps.FleshType.damageEffecter = oldEffecter;
            }

            // If for some reason they did not die from 9999 damage (magic shield?), just double-kill them the hard way.
            if (!pawn.Dead)
            {
                Find.BattleLog.RemoveEntry(log);
                pawn.Kill(dInfo, result?.hediffs?.FirstOrFallback());
            }
            else
            {
                result?.AssociateWithLog(log);
            }
        }
        else
        {
            // Magic kill...
            BodyPartDef partDef = args.BodyPartDef;
            DamageDef dmgDef = args.DamageDef ?? DamageDefOf.Cut;
            BodyPartRecord part = pawn.TryGetPartFromDef(partDef);
            Thing weapon = args.Weapon;

            // Does 0.01 damage, kills anyway.
            var dInfo = new DamageInfo(dmgDef, 0.01f, 0f, hitPart: part, instigator: killer, weapon: weapon?.def);
            pawn.Kill(dInfo);
        }

        // Apply corpse offset if required.
        var animator = pawn.TryGetAnimator();
        if (animator == null)
            return true;

        var animPart = animator.GetPawnBody(pawn);
        if (animPart == null)
            return true;

        var ss = animator.GetSnapshot(animPart);

        if (pawn.Corpse != null)
        {
            // Do corpse interpolation - interpolates the corpse to the correct position, after the animated position.
            Patch_Corpse_DrawAt.Interpolators[pawn.Corpse] = new CorpseInterpolate(pawn.Corpse, ss.GetWorldPosition());

            Patch_PawnRenderer_LayingFacing.OverrideRotations[pawn] = ss.GetWorldDirection();
        }
        else
        {
            Core.Warn($"{pawn} did not spawn a corpse after death, or the corpse was destroyed...");
        }

        // Update the pawn wiggler so that the pawn corpse matches the final animation state.
        // This does not change the body position, so when the animation ends and the corpse appears, the corpse often snaps to the center of the cell.
        // I don't know if there is any easy fix for this.
        var bodyRot = ss.GetWorldRotation();
        pawn.Drawer.renderer.wiggler.downedAngle = bodyRot;

        return true;
    }

    private static LogEntry_DamageResult CreateLog(RulePackDef def, Thing weapon, Pawn inst, Pawn vict)
    {
        //var log = new BattleLogEntry_MeleeCombat(rulePackGetter(this.maneuver), alwaysShow, this.CasterPawn, this.currentTarget.Thing, base.ImplementOwnerType, this.tool.labelUsedInLogging ? this.tool.label : "", (base.EquipmentSource == null) ? null : base.EquipmentSource.def, (base.HediffCompSource == null) ? null : base.HediffCompSource.Def, this.maneuver.logEntryDef);
        var log = new BattleLogEntry_MeleeCombat(def, true, inst, vict, ImplementOwnerTypeDefOf.Weapon, weapon?.Label, def: LogEntryDefOf.MeleeAttack);
        Find.BattleLog.Add(log);
        return log;
    }
}
