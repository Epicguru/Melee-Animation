using RimWorld;
using UnityEngine;
using Verse;

namespace AM.Stats;

public class StatWorker_ExecCooldown : StatWorker
{
    public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
    {
        // Get base value, just from def.
        var baseValue = base.GetValueUnfinalized(req, applyPostProcess);
        var pawn = req.Pawn ?? req.Thing as Pawn;
        if (pawn == null)
            return baseValue;

        bool isFriendly = !pawn.HostileTo(Faction.OfPlayerSilentFail);

        // Modify based on melee skill.
        if (isFriendly && Core.Settings.MeleeSkillExecCooldownFactor != 0)
            baseValue *= GetMeleeSkillCoef(pawn);

        // Modify based on mod settings.
        baseValue *= isFriendly ? Core.Settings.FriendlyExecCooldownFactor : Core.Settings.EnemyExecCooldownFactor;

        return baseValue;
    }

    public override string GetExplanationUnfinalized(StatRequest req, ToStringNumberSense numberSense)
    {
        string baseExp = base.GetExplanationUnfinalized(req, numberSense);
        var pawn = req.Pawn ?? req.Thing as Pawn;
        if (pawn == null)
            return baseExp;

        bool isFriendly = !pawn.HostileTo(Faction.OfPlayerSilentFail);
        float coef;

        // Modify based on melee skill.
        if (isFriendly && Core.Settings.MeleeSkillExecCooldownFactor != 0)
        {
            coef = GetMeleeSkillCoef(pawn);

            baseExp = $"{baseExp.TrimEnd()}\n" + "AM.Stats.SkillCoef".Trs(coef.ToString("F2"));
        }

        // Modify based on mod settings.
        coef = isFriendly ? Core.Settings.FriendlyExecCooldownFactor : Core.Settings.EnemyExecCooldownFactor;

        baseExp = $"{baseExp.TrimEnd()}\n" + $"AM.Stats.{(isFriendly ? "Friendly" : "Enemy")}Coef".Trs(coef.ToString("F2"));
        return baseExp;
    }

    private float GetMeleeSkillCoef(Pawn pawn)
    {
        const float NO_SKILL_COEF = 1.25f;
        const float MAX_SKILL_COEF = 0.75f;
        const float MIN_LEVEL = 0f;
        const float MAX_LEVEL = 20f;

        int level = pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
        float t = Mathf.InverseLerp(MIN_LEVEL, MAX_LEVEL, level);
        float skillCoef = Mathf.Lerp(NO_SKILL_COEF, MAX_SKILL_COEF, t);
        skillCoef = Mathf.LerpUnclamped(skillCoef, 1f, 1f - Core.Settings.MeleeSkillExecCooldownFactor);

        return skillCoef;
    }
}