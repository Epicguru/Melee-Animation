using RimWorld;
using System.Text;
using UnityEngine;
using Verse;

namespace AAM.Calculators
{
    public static class DuelUtility
    {
        private static StringBuilder str = new StringBuilder();

        /// <summary>
        /// Is the pawn not null, alive and not downed?
        /// </summary>
        public static bool IsValidBasic(Pawn p) => p != null && !p.Dead && !p.Downed;

        /// <summary>
        /// Does the pawn have a melee weapon in hands? (also implies being capable of violence, manipulation).
        /// This check includes the result of <see cref="IsValidBasic(Pawn)"/>.
        /// </summary>
        public static bool IsValidMeleeFighter(Pawn p) => IsValidBasic(p) && (p.equipment?.Primary?.def?.IsMeleeWeapon ?? false);

        /// <summary>
        /// Returns true if the execution pawn could theoretically execute the victim pawn if they were to duel.
        /// Ignores parts of their current state i.e. position on map.
        /// </summary>
        public static bool CanExecute(Pawn executioner, Pawn victim)
        {
            return IsValidMeleeFighter(executioner) && IsValidBasic(victim);
        }

        public static DuelOutcome MakeOutcome(Pawn a, Pawn b, bool doDebug = false)
        {
            DuelOutcome outcome = new DuelOutcome();
            outcome.PawnA = a;
            outcome.PawnB = b;
            float winLerp = 0;

            void MoveLerp(float change)
            {
                if (change == 0)
                    return;

                float old = winLerp;
                winLerp += change;
                if (doDebug)
                    str.Append("[WinLerp] ").Append(old).Append(" -> ").Append(winLerp.ToString()).Append(" (favours ").Append(old > winLerp ? 'A' : 'B').AppendLine(")");
            }

            if (doDebug)
            {
                str.Clear();
                str.AppendLine($"Pawn A: {a.NameFullColored} from {a.Faction?.Name ?? "<no-faction>"}");
                str.AppendLine($"Pawn B: {b.NameFullColored} from {b.Faction?.Name ?? "<no-faction>"}");
            }
            
            if (!IsValidBasic(a) || !IsValidBasic(b))
            {
                if (doDebug)
                {
                    str.Append($"Invalid (basic) pawn(s). Validity: ");
                    str.Append("A: ").Append(IsValidBasic(a)).Append(", ");
                    str.Append("B: ").Append(IsValidBasic(b));
                }
                outcome.Type = DuelOutcomeType.Invalid;
                outcome.GenDebug = doDebug ? str.ToString() : null;
                return outcome;
            }

            if (!IsValidMeleeFighter(a) && !IsValidMeleeFighter(b))
            {
                if (doDebug)
                {
                    str.Append($"Neither pawn is a valid melee fighter. The only possible outcome is Invalid; the duel should not take place.");
                }
                outcome.Type = DuelOutcomeType.Invalid;
                outcome.GenDebug = doDebug ? str.ToString() : null;
                return outcome;
            }

            int skillA = a.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
            int skillB = b.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
            int maxSkill = Mathf.Max(skillA, skillB);
            int skillBalance = skillB - skillA; // Positive when B is better, negative for A.

            // 20 skill vs 0 skill means +7.0 winLerp
            // 10 skill vs 0 skill means +3.5 winLerp
            float change = (skillBalance / 20f) * 7f;
            if (doDebug)
            {
                str.AppendLine();
                str.AppendLine("<b>Melee skills:</b>");
                str.Append("A: ").Append(skillA).Append(" (").Append(a.skills?.GetSkill(SkillDefOf.Melee)?.LevelDescriptor ?? "no-skill").AppendLine(")");
                str.Append("B: ").Append(skillB).Append(" (").Append(b.skills?.GetSkill(SkillDefOf.Melee)?.LevelDescriptor ?? "no-skill").AppendLine(")");
                str.Append("A vs B: ").AppendLine(skillBalance.ToString());
            }
            MoveLerp(change);

            const float HELPLESS_ENEMY_ADVANTAGE = +10;           
            if(!IsValidMeleeFighter(b))
            {
                // A has the clear advantage. They are armed and the enemy is unarmed!
                if (doDebug)
                    str.AppendLine("\n<b>Big advantage for A: B is not a melee fighter!</b>");
                MoveLerp(-HELPLESS_ENEMY_ADVANTAGE);
            }
            else if (!IsValidMeleeFighter(a))
            {
                // B has the clear advantage. They are armed and the enemy is unarmed!
                if (doDebug)
                    str.AppendLine("\n<b>Big advantage for B: A is not a melee fighter!</b>");
                MoveLerp(HELPLESS_ENEMY_ADVANTAGE);
            }
            else
            {
                var weaponA = a.equipment?.Primary;
                var weaponB = b.equipment?.Primary;

                float dpsA = weaponA?.GetStatValueForPawn(StatDefOf.MeleeWeapon_AverageDPS, a) ?? 0;
                float dpsB = weaponB?.GetStatValueForPawn(StatDefOf.MeleeWeapon_AverageDPS, b) ?? 0;

                const float MAX_DPS_BALANCE = 10f;
                const float DPS_DIFF_WEIGHT = 4f;

                float dpsBalance = Mathf.Clamp(dpsB - dpsA, -MAX_DPS_BALANCE, MAX_DPS_BALANCE);
                float dpsWeight = DPS_DIFF_WEIGHT * (dpsBalance / MAX_DPS_BALANCE);

                if (doDebug)
                {
                    str.AppendLine();
                    str.AppendLine("<b>Weapon DPS comparison:</b>");
                    str.Append("Weapon A: ").Append(weaponA?.LabelCap ?? "<null>").Append(", ").Append(dpsA).AppendLine(" DPS");
                    str.Append("Weapon B: ").Append(weaponB?.LabelCap ?? "<null>").Append(", ").Append(dpsB).AppendLine(" DPS");
                    str.Append("DPS balance: ").Append(dpsBalance).Append(" (clamped to +-").Append(MAX_DPS_BALANCE).AppendLine(")");
                    str.Append("Final DPS advantage: ").Append(dpsWeight).Append(" (clamped to +-").Append(DPS_DIFF_WEIGHT).AppendLine(")");
                }

                MoveLerp(dpsWeight);
            }            

            // Do randomness. If either pawn has a higher melee skill, randomness is reduced.
            const float MIN_RANDOM = 1f;
            const float MAX_RANDOM = 5f;
            float skillLerp = Mathf.Clamp01(maxSkill / 15f);
            float amplitude = Mathf.Lerp(MAX_RANDOM, MIN_RANDOM, skillLerp);
            float randomness = Rand.Range(-amplitude, amplitude);

            if (doDebug)
            {
                str.AppendLine();
                str.AppendLine("<b>Randomness:</b>");
                str.Append("Max melee skil: ").AppendLine(maxSkill.ToString());
                str.Append("Min/max random amplitude: ").Append(MIN_RANDOM).Append('/').AppendLine(MAX_RANDOM.ToString());
                str.Append("Melee skill lerp: ").AppendLine(skillLerp.ToString());
                str.Append("Final random amplitude: ").AppendLine(amplitude.ToString());
                str.Append("Final random offset: ").AppendLine(randomness.ToString());
            }
            MoveLerp(randomness);

            DuelOutcomeType type = DuelOutcomeType.Nothing;
            outcome.Winner = winLerp > 0 ? b : a;
            var loser = winLerp > 0 ? a : b;
            float abs = Mathf.Abs(winLerp);

            if (doDebug)
            {
                str.AppendLine();
                str.AppendLine("<b>Outcome:</b>");
                str.Append("Winner is ").Append(outcome.Winner.NameShortColored).Append(" with ").Append(abs).AppendLine(" certainty.");
            }

            const float MIN_EXECUTE = 6.5f;
            const float MIN_MAIM_OR_DOWN = 4.5f;
            const float MIN_HURT = 1.5f;

            if(abs >= MIN_EXECUTE)
            {
                if(doDebug)
                    str.Append("Certaintly is high enough for execution! ").Append(abs).Append(" > ").AppendLine(MIN_EXECUTE.ToString());
                if(CanExecute(outcome.Winner, loser))
                {
                    type = DuelOutcomeType.Execute;
                }
                else
                {
                    if (doDebug)
                        str.AppendLine(" ... but the winner is not capable of, or allowed to, execute the loser...");
                    type = DuelOutcomeType.MaimOrDown;
                }
            }
            else if(abs >= MIN_MAIM_OR_DOWN)
            {
                if (doDebug)
                    str.Append("Certaintly is high enough for maim-or-down! ").Append(abs).Append(" > ").AppendLine(MIN_MAIM_OR_DOWN.ToString());
                type = DuelOutcomeType.MaimOrDown;
            }
            else if(abs >= MIN_HURT)
            {
                if (doDebug)
                    str.Append("Certaintly is high enough for hurt! ").Append(abs).Append(" > ").AppendLine(MIN_MAIM_OR_DOWN.ToString());
                type = DuelOutcomeType.Hurt;
            }
            else
            {
                if (doDebug)
                    str.AppendLine("Certaintly is not high, outcome is neutral.");
                type = DuelOutcomeType.Nothing;
            }

            if (doDebug)
                str.Append("Final outcome type: ").AppendLine(type.ToString());

            outcome.GenDebug = doDebug ? str.ToString() : null;
            outcome.Type = type;
            outcome.Certainty = abs;
            return outcome;
        }
    }
}
