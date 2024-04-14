using AM.Grappling;
using AM.Reqs;
using RimWorld;
using Verse;

namespace AM.UniqueSkills
{
    public abstract class ExecutionUniqueSkillInstance : UniqueSkillInstance
    {
        private int? meleeLevelRequired;

        private int GetRequiredMeleeLevel()
        {
            return meleeLevelRequired ??= Def.GetData("MeleeLevelRequired", int.Parse);
        }

        public override bool IsEnabledForPawn(out string reasonWhyNot)
        {
            var filter = Def.animation.weaponFilter;
            var pawnWeapon = Pawn.GetFirstMeleeWeapon();
            var input = new ReqInput(pawnWeapon?.def);

            // Check required weapon:
            if (pawnWeapon == null || !filter.Evaluate(input))
            {
                reasonWhyNot = "AM.Skill.BadWeapon".Trs();
                return false;
            }

            // Check melee level:
            int required = GetRequiredMeleeLevel();
            int current;
            if (required > 0 && (current = Pawn.skills.GetSkill(SkillDefOf.Melee).Level) < required)
            {
                reasonWhyNot = "AM.Skill.Gilgamesh.LowMelee".Translate(current, required);
                return false;
            }

            reasonWhyNot = null;
            return true;
        }

        public override bool TryTrigger(in LocalTargetInfo target)
        {
            return true;
        }

        public override void OnAnimationStarted(AnimRenderer animation)
        {
            if (animation.Def == Def.animation && IsEnabledForPawn(out _))
            {
                Core.Log($"Putting {this} skill on cooldown");
                TickLastTriggered = GenTicks.TicksGame;
            }

            base.OnAnimationStarted(animation);
        }

        public override string CanTriggerOn(in LocalTargetInfo target)
        {
            // Skill cooldown.
            float cooldownRemaining = GetCooldownSecondsLeft();
            if (cooldownRemaining > 0)
                return "AM.Skill.OnCooldown".Translate(cooldownRemaining.ToString("F1"));

            // Exec cooldown.
            cooldownRemaining = Pawn.GetMeleeData().GetExecuteCooldownMax() - Pawn.GetMeleeData().TimeSinceExecuted;
            if (cooldownRemaining > 0)
                return "AM.Skill.OnCooldownExec".Translate(cooldownRemaining.ToString("F1"));

            // Check not dead, downed etc.
            var pawn = target.Pawn;
            if (pawn == null || pawn.Downed || pawn.Dead || !pawn.Spawned || pawn.IsInAnimation() || GrabUtility.IsBeingTargetedForGrapple(pawn))
                return "AM.Skill.BadTargetState".Translate();

            return null;
        }
    }
}
