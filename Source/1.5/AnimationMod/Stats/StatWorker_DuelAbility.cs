using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace AM.Stats;

public class StatWorker_DuelAbility : StatWorker
{
    public override bool ShouldShowFor(StatRequest req)
    {
        var pawn = req.Pawn ?? req.Thing as Pawn;
        return pawn != null && pawn.def.race.ToolUser;
    }

    public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
    {
        return GetStatValue(req, ToStringNumberSense.Absolute, null, applyPostProcess);
    }

    public override string GetExplanationUnfinalized(StatRequest req, ToStringNumberSense numberSense)
    {
        var str = new StringBuilder();
        GetStatValue(req, numberSense, str);
        return str.ToString();
    }

    private float GetStatValue(in StatRequest req, ToStringNumberSense numberSense, StringBuilder str, bool applyPostProcess = true)
    {
        var pawn = req.Pawn ?? req.Thing as Pawn;
        if (pawn == null)
        {
            str?.Append("Pawn is null, invalid.");
            return 0f;
        }

        int meleeSkill = pawn.skills?.GetSkill(SkillDefOf.Melee).Level ?? 0;
        Thing weapon = pawn.GetFirstMeleeWeapon();
        float? meleeDps = weapon?.GetStatValue(StatDefOf.MeleeWeapon_AverageDPS);
        float consciousness = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Consciousness) ?? 0f;
        float manipulation = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Manipulation) ?? 0f;

        // Base value.
        float baseline = GetBaseValueFor(req);
        str?.AppendLine("AM.Stats.BaseValue".Translate(baseline.ToStringByStyle(ToStringStyle.PercentOne, numberSense)));

        // Melee skill.
        float meleeSkillOffset = Remap(0f, 20f, -0.3f, 0.6f, meleeSkill);
        if (meleeSkillOffset != 0f)
            str?.AppendLine("AM.Stats.MeleeSkillOffset".Translate(meleeSkill, meleeSkillOffset.ToStringByStyle(ToStringStyle.PercentZero, ToStringNumberSense.Offset)));

        // Consciousness.
        float consciousnessOffset = consciousness < 1f ? Remap(0f, 1f, -0.8f, 0f, consciousness) : Remap(1f, 2f, 0f, 0.2f, consciousness);
        if (consciousnessOffset != 0f)
            str?.AppendLine("AM.Stats.ConsciousnessOffset".Translate(consciousness.ToStringByStyle(ToStringStyle.PercentZero), consciousnessOffset.ToStringByStyle(ToStringStyle.PercentZero, ToStringNumberSense.Offset)));

        // Manipulation
        float manipulationOffset = manipulation < 1f ? Remap(0f, 1f, -0.5f, 0f, manipulation) : Remap(1f, 2f, 0f, 0.15f, manipulation);
        if (manipulationOffset != 0f)
            str?.AppendLine("AM.Stats.ManipulationOffset".Translate(manipulation.ToStringByStyle(ToStringStyle.PercentZero), manipulationOffset.ToStringByStyle(ToStringStyle.PercentOne, ToStringNumberSense.Offset)));

        // Melee Weapon DPS
        float dpsOffset = 0f;
        if (meleeDps != null)
        {
            dpsOffset = Remap(8f, 50f, 0f, 0.3f, meleeDps.Value);
            if (dpsOffset != 0)
            {
                str?.AppendLine("AM.Stats.WeaponDPSCoef".Translate(meleeDps.Value.ToStringByStyle(ToStringStyle.FloatOne), dpsOffset.ToStringByStyle(ToStringStyle.PercentOne, ToStringNumberSense.Offset)));
            }
        }

        // No weapon negative.
        float noWeaponOffset = weapon == null ? -0.9f : 0f;
        if (noWeaponOffset != 0)
            str?.AppendLine("AM.Stats.NoWeapon".Translate(noWeaponOffset.ToStringByStyle(ToStringStyle.PercentZero, ToStringNumberSense.Offset)));

        // Friendly bonus
        float friendBonus = 0f;
        if ((pawn.IsColonist || pawn.IsSlaveOfColony) && Core.Settings.FriendlyPawnDuelBonus != 0f)
        {
            friendBonus = Core.Settings.FriendlyPawnDuelBonus;
            str?.AppendLine("AM.Stats.FriendlyPawnBonus".Translate(friendBonus.ToStringByStyle(ToStringStyle.PercentZero, ToStringNumberSense.Offset)));
        }

        float final = Mathf.Max(0f, baseline + meleeSkillOffset + consciousnessOffset + manipulationOffset + dpsOffset + noWeaponOffset + friendBonus);
        return final;
    }

    private float Remap(float a, float b, float c, float d, float value)
    {
        return Mathf.Lerp(c, d, Mathf.InverseLerp(a, b, value));
    }
}
