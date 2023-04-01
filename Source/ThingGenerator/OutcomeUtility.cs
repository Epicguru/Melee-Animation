using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using AM.Patches;
using RimWorld;
using UnityEngine;
using Verse;
using static UnityEngine.GraphicsBuffer;

namespace AM;

/// <summary>
/// Helper class to determine and then execute the outcome of duels and executions.
/// </summary>
public static class OutcomeUtility
{
    public ref struct AdditionalArgs
    {
        public RulePackDef LogGenDef;
        public Thing Weapon;
        public float TargetDamageAmount;
    }

    [DebugAction("Melee Animation", "Compare Lethality", allowedGameStates = AllowedGameStates.PlayingOnMap, actionType = DebugActionType.ToolMapForPawns)]
    private static void CompareLethalityDebug(Pawn pawn)
    {
        Pawn selected = Find.Selector.SelectedPawns.FirstOrDefault();
        if (selected == null || selected == pawn)
        {
            Messages.Message("Select another pawn before using the debug tool.", MessageTypeDefOf.RejectInput, false);
            return;
        }

        Core.Log($"{selected}, {pawn}");

        float a  = selected.GetStatValue(AM_DefOf.AM_Lethality);
        float a2 = selected.GetStatValue(AM_DefOf.AM_DuelAbility);
        float b  = pawn.GetStatValue(AM_DefOf.AM_Lethality);
        float b2 = pawn.GetStatValue(AM_DefOf.AM_DuelAbility);
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
        float aL = a.GetStatValue(AM_DefOf.AM_DuelAbility);
        float bL = b.GetStatValue(AM_DefOf.AM_DuelAbility);
        float diff = aL - bL;

        return Mathf.Clamp01((float)normal.LeftProbability(diff));;
    }

    public static ExecutionOutcome GenerateRandomOutcome(Pawn attacker, Pawn victim) => GenerateRandomOutcome(attacker, victim, out _);

    public static ExecutionOutcome GenerateRandomOutcome(Pawn attacker, Pawn victim, out float pct)
    {
        Debug.Assert(attacker != null);
        Debug.Assert(victim != null);

        // Get lethality, adjusted by settings.
        float aL = attacker.GetStatValue(AM_DefOf.AM_Lethality);

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
    public static ExecutionOutcome GenerateRandomOutcome(DamageDef dmgDef, Pawn victim, BodyPartRecord bodyPart, float weaponPen, float lethality, out float pct)
    {
        //if (lethality <= 0f)
        //{
        //    pct = 1f;
        //    return ExecutionOutcome.Damage;
        //}

        //if (Rand.Chance(lethality))
        //{
        //    pct = lethality;
        //    return ExecutionOutcome.Kill;
        //}

        //pct = (1f - lethality) * 0.5f;
        //return Rand.Chance(0.5f) ? ExecutionOutcome.Down : ExecutionOutcome.Damage;

        /*
         * Flow:
         * If rand(lethality), kill is attempted:
         *  - Check victim armor pct. against weapon pen. If weapon outclasses armor, continue with kill.
         *  - If armor outclasses weapon, down instead of kill.
         * 50% chance to attempt to down:
         *  - If weapon pen is above 50% chance to pen armor, continue to down.
         *  - Otherwise fall back to injure.
         */

        var armorStat = dmgDef?.armorCategory?.armorRatingStat ?? StatDefOf.ArmorRating_Sharp;
        float totalArmor = GetChanceToPenAprox(victim, bodyPart, armorStat, weaponPen);


    }

    public static float GetChanceToPenAprox(Pawn pawn, BodyPartRecord bodyPart, StatDef armorType, float armorPen)
    {
        float chanceToPen = 1f;

        // Get skin & hediff chance-to-pen.
        chanceToPen *= Mathf.Clamp01(1f - (pawn.GetStatValue(armorType) - armorPen));

        if (pawn?.apparel == null)
            return Mathf.Clamp01(chanceToPen + 0.25f);

        // Get apparel chance-to-pen.
        foreach (var a in pawn.apparel.WornApparel)
        {
            if (!a.def.apparel.CoversBodyPart(bodyPart))
                continue;

            var armor = a.GetStatValue(armorType);
            if (armor <= 0)
                continue;

            chanceToPen *= Mathf.Clamp01(1f - (armor - armorPen));
        }

        return Mathf.Clamp01(chanceToPen + 0.25f);
    }

    private static bool Damage(Pawn attacker, Pawn pawn, in AdditionalArgs args)
    {
        // Damage but do not kill or down the target.

        float dmgToDo = args.TargetDamageAmount;
        float totalDmgDone = 0;

        bool WouldBeInvalidResult(HediffDef hediff, float dmg, BodyPartRecord bp)
        {
            return pawn.health.WouldLosePartAfterAddingHediff(hediff, bp, dmg) ||
                   pawn.health.WouldBeDownedAfterAddingHediff(hediff, bp, dmg) ||
                   pawn.health.WouldDieAfterAddingHediff(hediff, bp, dmg);
        }

        for (int i = 0; i < 50; i++)
        {
            var verbProps = args.Weapon.def.Verbs.First();
            var tool = args.Weapon.def.tools.RandomElementByWeight(t => t.chanceFactor);

            float dmg = verbProps.AdjustedMeleeDamageAmount(tool, attacker, args.Weapon, null);
            if (dmg > dmgToDo)
                dmg = dmgToDo;
            dmgToDo -= dmg;
            float armorPenetration = verbProps.AdjustedArmorPenetration(tool, attacker, args.Weapon, null);

            DamageDef def = verbProps.meleeDamageDef ?? DamageDefOf.Blunt;
            var bodyPartGroupDef = verbProps.AdjustedLinkedBodyPartsGroup(tool);

            for (int j = 0; j < 5; j++)
            {
                var part = pawn.health.hediffSet.GetRandomNotMissingPart(def, BodyPartHeight.Middle, BodyPartDepth.Outside);

                if (dmg < 1f) {
                    dmg = 1f;
                    def = DamageDefOf.Blunt;
                }

                if (WouldBeInvalidResult(def.hediff, dmg, part))
                    continue;

                ThingDef source = args.Weapon.def;
                Vector3 direction = (pawn.Position - attacker.Position).ToVector3();
                DamageInfo damageInfo = new DamageInfo(def, dmg, armorPenetration, -1f, attacker, null, source);
                damageInfo.SetWeaponBodyPartGroup(bodyPartGroupDef);
                damageInfo.SetAngle(direction);
                damageInfo.SetIgnoreInstantKillProtection(false);
                damageInfo.SetAllowDamagePropagation(false);

                var info = pawn.TakeDamage(damageInfo);
                totalDmgDone += info.totalDamageDealt;
                if (pawn.Dead || pawn.Downed) {
                    Core.Error($"Accidentally killed or downed {pawn} when attempting to just injure: tried to deal {dmg:F1} {def} dmg to {part}. Storyteller, difficulty, hediffs, or mods could have modified the damage to cause this.");
                    return false;
                }
                break;
            }

            if (dmgToDo <= 0)
                break;
        }

        Core.Log($"Dealt {totalDmgDone:F2} pts of dmg as part of injury.");
        return true;
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
        // Give the downed hediff.
        var h = pawn.health.AddHediff(AM_DefOf.AM_KnockedOut);

        if (h == null)
            Core.Error($"Failed to give {pawn} the knocked out hediff!");
        
        return h != null;
    }

    private static bool Kill(Pawn killer, Pawn pawn, in AdditionalArgs args)
    {
        if (Core.Settings.ExecutionsCanDestroyBodyParts)
        {
            BodyPartDef partDef = args.BodyPartDef;
            DamageDef dmgDef = args.DamageDef ?? DamageDefOf.Cut;
            RulePackDef logDef = args.LogGenDef ?? AM_DefOf.AM_Execution_Generic;
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
