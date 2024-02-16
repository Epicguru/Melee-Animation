using System;
using System.Collections.Generic;
using System.Linq;
using AM.Patches;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Verse;

namespace AM.Outcome;

/// <summary>
/// Helper class to determine and then execute the outcome of duels and executions.
/// </summary>
public static class OutcomeUtility
{
    public static IOutcomeWorker OutcomeWorker = new VanillaOutcomeWorker();

    public static readonly Func<float, float, bool> GreaterThan = (x, y) => x > y;
    public static readonly Func<float, float, bool> LessThan = (x, y) => x < y;

    public static readonly Func<PossibleMeleeAttack, float> Pen = a => a.ArmorPen;
    public static readonly Func<PossibleMeleeAttack, float> Dmg = a => a.Damage;
    public static readonly Func<PossibleMeleeAttack, float> PenXDmg = a => a.Damage * a.ArmorPen;

    [UsedImplicitly]
    [TweakValue("Melee Animation")]
#pragma warning disable CS0649 // Field 'OutcomeUtility.debugLogExecutionOutcome' is never assigned to, and will always have its default value false
    private static bool debugLogExecutionOutcome;
#pragma warning restore CS0649 // Field 'OutcomeUtility.debugLogExecutionOutcome' is never assigned to, and will always have its default value false

    public ref struct AdditionalArgs
    {
        public DamageDef DamageDef;
        public BodyPartDef BodyPartDef;
        public RulePackDef LogGenDef;
        public ThingWithComps Weapon;
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

            case ExecutionOutcome.Failure:
                return Failure(attacker, target, args);

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

        return Mathf.Clamp01((float)normal.LeftProbability(diff));
    }

    public static ExecutionOutcome GenerateRandomOutcome(Pawn attacker, Pawn victim, bool canFail, ProbabilityReport report = null)
    {
        // Get lethality, adjusted by settings.
        float aL = attacker.GetStatValue(AM_DefOf.AM_Lethality);
        int meleeSkill = attacker.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
        var weapon = attacker.GetFirstMeleeWeapon();
        var mainAttack = SelectMainAttack(OutcomeWorker.GetMeleeAttacksFor(weapon, attacker));
        var damageDef = mainAttack.DamageDef;
        var corePart = GetCoreBodyPart(victim);

        return GenerateRandomOutcome(damageDef, victim, corePart, mainAttack.ArmorPen, meleeSkill, aL, canFail, report);
    }

    /// <summary>
    /// Generates a random execution outcome based on an lethality value.
    /// </summary>
    public static ExecutionOutcome GenerateRandomOutcome(DamageDef dmgDef, Pawn victim, BodyPartRecord bodyPart, float weaponPen, float attackerMeleeSkill, float lethality, bool canFail, ProbabilityReport report = null)
    {
        static void Log(string msg)
        {
            if (debugLogExecutionOutcome)
                Core.Log(msg);
        }

        var outcome = ExecutionOutcome.Nothing;

        // Before anything else: check for failure chance.
        float chanceToFail = canFail ? Mathf.Clamp01(RemapClamped(0, 20, Core.Settings.ChanceToFailMinSkill, Core.Settings.ChanceToFailMaxSkill, attackerMeleeSkill)) : 0f;
        Log($"Chance to fail: {chanceToFail:P1} based on melee skill {attackerMeleeSkill:F1}");
        bool willFail = Rand.Chance(chanceToFail);
        if (willFail && report == null)
        {
            Log("Outcome: Failed.");
            outcome = ExecutionOutcome.Failure;
            return outcome;
        }
        if (report != null)
            report.FailureChance = chanceToFail;

        // Armor calculations for down or kill.
        var armorStat = dmgDef?.armorCategory?.armorRatingStat ?? StatDefOf.ArmorRating_Sharp;
        Log($"Armor stat: {armorStat}, weapon pen: {weaponPen:F2}, lethality: {lethality:F2}");
        float armorMulti = Core.Settings.ExecutionArmorCoefficient;
        float chanceToPen = OutcomeWorker.GetChanceToPenAprox(victim, bodyPart, armorStat, weaponPen);
        Log($"Chance to pen (base): {chanceToPen:P1}");
        if (armorMulti > 0f)
            chanceToPen /= armorMulti;
        else
            chanceToPen = 1f;
        Log($"Chance to pen (post-settings): {chanceToPen:P1}");
        bool canPen = Rand.Chance(chanceToPen);
        Log($"Random will pen outcome: {canPen}");

        // Cap chance to pen to make percentage calculations work.
        chanceToPen = Mathf.Clamp01(chanceToPen);

        // Calculate kill chance based on lethality and settings.
        bool preventKill = Core.Settings.ExecutionsOnFriendliesAreNotLethal && (victim.IsColonist || victim.IsSlaveOfColony || victim.IsPrisonerOfColony || victim.Faction == Faction.OfPlayerSilentFail);
        bool attemptKill = Rand.Chance(lethality);
        Log($"Prevent kill: {preventKill}");
        Log($"Attempt kill: {attemptKill} (from random lethality chance)");

        // Cap lethality to 100% because otherwise it messes up percentage calculations.
        lethality = Mathf.Clamp01(lethality);

        float preventKillCoef = preventKill ? 0f : 1f;
        if (report != null)
            report.KillChance = preventKillCoef * chanceToPen * lethality * (1 - chanceToFail); // Absolute chance to kill.

        if (canPen && !preventKill && attemptKill)
        {
            Log("Killed!");
            outcome = ExecutionOutcome.Kill;
            if (report == null)
                return outcome;
        }

        Log("Moving on to down or injure...");
        float downChance = RemapClamped(4, 20, 0.2f, 0.9f, attackerMeleeSkill);
        Log(canPen ? $"Chance to down, based on melee skill of {attackerMeleeSkill:N1}: {downChance:P1}" : "Cannot down, pen chance failed. Will damage.");
        if (report != null)
        {
            report.DownChance = (1 - report.KillChance - report.FailureChance) * chanceToPen * downChance;
            report.InjureChance = (1 - report.KillChance - report.DownChance - report.FailureChance);

            float sum = report.KillChance + report.DownChance + report.InjureChance + report.FailureChance;
            if (Math.Abs(sum - 1f) > 0.001f)
                Core.Warn($"Bad percentage calculation ({sum})! Please tell the developer he is an idiot.");
        }

        if (outcome == ExecutionOutcome.Nothing && canPen && Rand.Chance(downChance))
        {
            Log("Downed");
            outcome = ExecutionOutcome.Down;
            if (report == null)
                return outcome;
        }

        // Damage!
        if (outcome == ExecutionOutcome.Nothing)
        {
            Log("Damaged");
            outcome = ExecutionOutcome.Damage;
            if (report == null)
                return outcome;
        }

        report?.Normalize();
        return outcome;
    }

    public static float RemapClamped(float baseA, float baseB, float newA, float newB, float value)
    {
        float t = Mathf.InverseLerp(baseA, baseB, value);
        return Mathf.Lerp(newA, newB, t);
    }

    private static BodyPartRecord GetCoreBodyPart(Pawn pawn) => pawn.def.race.body.corePart;

    [UsedImplicitly]
    [DebugOutput("Melee Animation")]
    private static void LogAllMeleeWeaponVerbs()
    {
        var meleeWeapons = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.IsMeleeWeapon);
        var created = new HashSet<ThingWithComps>();
        foreach (var def in meleeWeapons)
        {
            created.Add(ThingMaker.MakeThing(def) as ThingWithComps);
        }

        static string VerbToString(Verb verb, ThingWithComps weapon = null)
        {
            float dmg = OutcomeWorker.GetDamage(weapon, verb, null);
            float ap = OutcomeWorker.GetPen(weapon, verb, null);
            var armorSt = verb.GetDamageDef()?.armorCategory?.armorRatingStat ?? StatDefOf.ArmorRating_Sharp;
            return $"{verb}: {verb.GetDamageDef()} (dmg: {dmg:F2}, ap: {ap:F2}, armor: {armorSt})";
        }

        static string AllVerbs(ThingWithComps t)
        {
            return string.Join("\n", GetEq(t).AllVerbs.Select(v => VerbToString(v)));
        }

        static CompEquippable GetEq(ThingWithComps td) => td.GetComp<CompEquippable>();

        TableDataGetter<ThingWithComps>[] table = new TableDataGetter<ThingWithComps>[4];
        table[0] = new TableDataGetter<ThingWithComps>("Def Name", d => d.def.defName);
        table[1] = new TableDataGetter<ThingWithComps>("Name", d => d.LabelCap);
        table[2] = new TableDataGetter<ThingWithComps>("Main Attack", d => VerbToString(SelectMainAttack(OutcomeWorker.GetMeleeAttacksFor(d, null)).Verb));
        table[3] = new TableDataGetter<ThingWithComps>("All Verbs", AllVerbs);

        DebugTables.MakeTablesDialog(created, table);

        foreach (var t in created)
            t.Destroy();
    }

    public static PossibleMeleeAttack SelectMainAttack(IEnumerable<PossibleMeleeAttack> attacks) => SelectMainAttack(attacks, Dmg, GreaterThan);

    public static PossibleMeleeAttack SelectMainAttack(IEnumerable<PossibleMeleeAttack> attacks, Func<PossibleMeleeAttack, float> valueSelector, Func<float, float, bool> compareFunc)
    {
        // Highest pen?
        // highest damage?
        // Pen x damage?

        PossibleMeleeAttack selected = default;
        float? record = null;

        foreach (var a in attacks)
        {
            float value = valueSelector(a);

            if (record == null)
            {
                selected = a;
                record = value;
                continue;
            }

            if (!compareFunc(value, record.Value))
                continue;

            selected = a;
            record = value;
        }

        return selected;
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
            // TODO check does this correctly get the right verbs even if the melee weapon is a sidearm?
            Verb verb;
            int limit = 1000;
            do
            {
                verb = attacker.meleeVerbs.TryGetMeleeVerb(pawn);
                if (limit-- == 0)
                {
                    Core.Error($"Failed to find random verb for weapon '{args.Weapon}' on pawn {pawn}. May be a result of an optimization mod or bug.\n" +
                        $"The possible verbs are {string.Join(", ", attacker.meleeVerbs.GetUpdatedAvailableVerbsList(false).Where(v => v.IsMeleeAttack).Select(v => v.verb))}");
                    return false;
                }

                // Force verb refresh if required.
                if (verb.EquipmentSource != args.Weapon)
                {
                    attacker.meleeVerbs.ChooseMeleeVerb(pawn);
                }

            } while (verb.EquipmentSource != args.Weapon);

            float dmg = OutcomeWorker.GetDamage(args.Weapon, verb, attacker);
            if (dmg > dmgToDo)
                dmg = dmgToDo;
            dmgToDo -= dmg;
            float armorPenetration = OutcomeWorker.GetPen(args.Weapon, verb, attacker);

            if (debugLogExecutionOutcome)
                Core.Log($"Using verb {verb} to hit for {dmg:F1} dmg with {armorPenetration:F2} pen. Rem: {dmgToDo}");

            DamageDef def = verb.GetDamageDef();
            var bodyPartGroupDef = verb.verbProps.AdjustedLinkedBodyPartsGroup(verb.tool);

            for (int j = 0; j < 5; j++)
            {
                var part = pawn.health.hediffSet.GetRandomNotMissingPart(def, BodyPartHeight.Middle, BodyPartDepth.Outside);

                if (dmg < 1f)
                {
                    Core.Warn($"Very low damage of {dmg:F3} with verb {verb}. Changing to 1 blunt damage.");
                    dmg = 1f;
                    def = DamageDefOf.Blunt;
                }

                if (WouldBeInvalidResult(def.hediff, dmg, part))
                {
                    if (j == 4)
                        Core.Warn($"Failed to find any hit for {dmg:F2} dmg that would not kill, down or amputate part on {pawn}. Will keep trying for the remaining {dmgToDo:F2} dmg.");
                    continue;
                }

                OutcomeWorker.PreDamage(verb);

                ThingDef source = args.Weapon.def;
                Vector3 direction = (pawn.Position - attacker.Position).ToVector3();
                DamageInfo damageInfo = new DamageInfo(def, dmg, armorPenetration, -1f, attacker, null, source);
                damageInfo.SetWeaponBodyPartGroup(bodyPartGroupDef);
                damageInfo.SetAngle(direction);
                damageInfo.SetIgnoreInstantKillProtection(false);
                damageInfo.SetAllowDamagePropagation(false);

                var info = pawn.TakeDamage(damageInfo);
                if (debugLogExecutionOutcome)
                    Core.Log($"Hit {part.LabelCap} for {info.totalDamageDealt:F2}/{dmg:F2} dmg, mitigated");
                totalDmgDone += info.totalDamageDealt;
                if (pawn.Dead || pawn.Downed) {
                    Core.Error($"Accidentally killed or downed {pawn} when attempting to just injure: tried to deal {dmg:F1} {def} dmg to {part.LabelCap}. Storyteller, difficulty, hediffs, or mods could have modified the damage to cause this.");
                    return false;
                }
                break;
            }

            if (dmgToDo <= 0)
                break;
        }

        Core.Log($"Dealt {totalDmgDone:F2} pts of dmg as part of injury to {pawn} (aimed to deal {args.TargetDamageAmount:F2}).");
        return true;
    }

    private static bool Down(Pawn attacker, Pawn pawn, in AdditionalArgs _)
    {
        // Give the downed hediff.
        var h = pawn.health.AddHediff(AM_DefOf.AM_KnockedOut);

        if (h == null)
            Core.Error($"Failed to give {pawn} the knocked out hediff!");
        
        return h != null;
    }

    private static bool Failure(Pawn attacker, Pawn target, in AdditionalArgs _)
    {
        // Stun the attacker for a bit.
        attacker.stances?.stunner?.StunFor(60 * 3, attacker, false);
        return true;
    }

    private static bool IsDeathless(Pawn pawn)
    {
        return pawn.genes?.HasGene(GeneDefOf.Deathless) ?? false;
    }

    private static bool Kill(Pawn killer, Pawn pawn, in AdditionalArgs args)
    {
        bool isDeathless = IsDeathless(pawn);

        if (Core.Settings.ExecutionsCanDestroyBodyParts || isDeathless)
        {
            BodyPartDef partDef = args.BodyPartDef;
            DamageDef dmgDef = args.DamageDef ?? DamageDefOf.Cut;
            RulePackDef logDef = args.LogGenDef ?? AM_DefOf.AM_Execution_Generic;
            Thing weapon = args.Weapon;

            // Don't allow destroying the head because this would really kill the 
            if (isDeathless && partDef == BodyPartDefOf.Head)
            {
                partDef = GetCoreBodyPart(pawn).def;
                Core.Log($"Since {pawn} is deathless, the killing damage has been changed from targeting the Head to instead hit the {partDef}.");
            }

            BodyPartRecord part = pawn.TryGetPartFromDef(partDef);
            var dInfo = new DamageInfo(dmgDef, 99999, 99999, hitPart: part, instigator: killer, weapon: weapon?.def);
            var log = CreateLog(logDef, weapon, killer, pawn);
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
            if (!pawn.Dead && !isDeathless)
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
        else if (!isDeathless)
        {
            Core.Warn($"{pawn} did not spawn a corpse after death, or the corpse was destroyed...");
        }

        // Update the pawn wiggler so that the pawn corpse matches the final animation state.
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

    public class ProbabilityReport
    {
        public float FailureChance { get; set; }
        public float DownChance { get; set; }
        public float InjureChance { get; set; }
        public float KillChance { get; set; }

        public void Normalize()
        {
            float sum = DownChance + InjureChance + KillChance + FailureChance;
            if (sum == 0f)
                return;

            DownChance /= sum;
            InjureChance /= sum;
            KillChance /= sum;
            FailureChance /= sum;
        }

        private static string InColor(float pct)
        {
            Color c = Color.Lerp(Color.red, Color.green, pct);
            string hex = ColorUtility.ToHtmlStringRGB(c);
            return $"<color={hex}>{pct*100:F0}%</color>";
        }

        public override string ToString() => $"{"AM.ProbReport.Kill".Trs()}: {InColor(KillChance)}\n{"AM.ProbReport.Down".Trs()}: {InColor(DownChance)}\n{"AM.ProbReport.Injure".Trs()}: {InColor(InjureChance)}\n{"AM.ProbReport.Fail".Trs()}: {InColor(FailureChance)}";
    }
}
