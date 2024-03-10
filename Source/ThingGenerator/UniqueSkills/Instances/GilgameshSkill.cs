using RimWorld;
using UnityEngine;
using Verse;

namespace AM.UniqueSkills.Instances;

public class GilgameshSkill : ChanneledUniqueSkillInstance
{
    private int? psyLevelRequired;
    private int? meleeLevelRequired;

    private int GetRequiredPsyLevel()
    {
        return psyLevelRequired ??= Def.GetData("PsyLevelRequired", int.Parse);
    }

    private int GetRequiredMeleeLevel()
    {
        return meleeLevelRequired ??= Def.GetData("MeleeLevelRequired", int.Parse);
    }

    public override bool IsEnabledForPawn(out string reasonWhyNot)
    {
        int psyLevel = Pawn.GetPsylinkLevel();
        int required = GetRequiredPsyLevel();
        if (psyLevel < required)
        {
            reasonWhyNot = "AM.Skill.Gilgamesh.LowPsy".Translate(psyLevel, required);
            return false;
        }

        int meleeLevel = Pawn.skills.GetSkill(SkillDefOf.Melee).Level;
        required = GetRequiredMeleeLevel();
        if (meleeLevel < required)
        {
            reasonWhyNot = "AM.Skill.Gilgamesh.LowMelee".Translate(meleeLevel, required);
            return false;
        }

        reasonWhyNot = null;
        return true;
    }

    public override void OnAnimationStart(AnimRenderer animator)
    {
        // Nothing to do.
        Core.Log($"{Pawn} started gilgamesh attack.");
    }

    public override void OnAnimationComplete(bool didRunToEnd)
    {
        if (!didRunToEnd)
            return;

        if (Pawn.Dead || Pawn.Downed || !Pawn.Spawned)
        {
            Core.Log($"{Pawn} was in invalid state, will not apply psychic coma.");
            return;
        }

        // Add Psychic Coma hediff.
        var coma = DefDatabase<HediffDef>.GetNamed("PsychicComa");
        var instance = HediffMaker.MakeHediff(coma, Pawn);
        var timer = instance.TryGetComp<HediffComp_Disappears>();
        if (timer != null)
        {
            float min = Def.GetData("MinPsyComaDays", float.Parse, 2);
            float max = Def.GetData("MaxPsyComaDays", float.Parse, 3);
            float daysToRemove = Rand.Range(min, max);
            timer.ticksToDisappear = Mathf.RoundToInt(GenDate.TicksPerDay * daysToRemove);

            if (timer.ticksToDisappear <= 0)
                return;

            Core.Log($"Set psy coma to {daysToRemove:F2} days, {timer.ticksToDisappear} ticks.");
        }
        else
        {
            Core.Warn("PsychicComa hediff is missing the usual HediffComp_Disappears component, why did another mod remove it?");
        }

        Pawn.health.AddHediff(instance);
    }
}