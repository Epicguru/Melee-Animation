using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace AM.Stats;

public class StatWorker_Lethality : StatWorker
{
    public static readonly Dictionary<ToolCapacityDef, float> CapacityToLethalityOffset = new Dictionary<ToolCapacityDef, float>()
    {
        { AM_DefOf.Cut, 0.2f },
        { AM_DefOf.Stab, 0.3f },
        { AM_DefOf.Blunt, -0.75f },
    };

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
        ToolCapacityDef capMod = null;
        float capOffset = float.MinValue;
        if (weapon?.def?.tools != null)
        {
            foreach (var cap in weapon.def.tools.SelectMany(t => t.capacities))
            {
                if (!CapacityToLethalityOffset.TryGetValue(cap, out float found))
                    continue;

                if (found > capOffset)
                {
                    capOffset = found;
                    capMod = cap;
                }
            }
        }
        if (capMod == null)
            capOffset = 0f;

        float? meleeDps = weapon?.GetStatValue(StatDefOf.MeleeWeapon_AverageDPS);

        float consciousness = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Consciousness) ?? 0f;
        float manipulation = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Manipulation) ?? 0f;

        // Base value.
        float baseline = GetBaseValueFor(req);
        str?.AppendLine("AM.Stats.BaseValue".Translate(baseline.ToStringByStyle(ToStringStyle.PercentOne, numberSense)));

        // Melee skill.
        float meleeSkillOffset = Remap(0f, 20f, -0.2f, 0.4f, meleeSkill);
        if (meleeSkillOffset != 0f)
            str?.AppendLine("AM.Stats.MeleeSkillOffset".Translate(meleeSkill, meleeSkillOffset.ToStringByStyle(ToStringStyle.PercentZero, ToStringNumberSense.Offset)));

        // Consciousness.
        float consciousnessOffset = consciousness < 1f ? Remap(0f, 1f, -0.8f, 0f, consciousness) : Remap(1f, 2f, 0f, 0.25f, consciousness);
        if (consciousnessOffset != 0f)
            str?.AppendLine("AM.Stats.ConsciousnessOffset".Translate(consciousness.ToStringByStyle(ToStringStyle.PercentZero), consciousnessOffset.ToStringByStyle(ToStringStyle.PercentZero, ToStringNumberSense.Offset)));

        // Manipulation
        float manipulationOffset = manipulation < 1f ? Remap(0f, 1f, -0.5f, 0f, manipulation) : Remap(1f, 2f, 0f, 0.2f, manipulation);
        if (manipulationOffset != 0f)
            str?.AppendLine("AM.Stats.ManipulationOffset".Translate(manipulation.ToStringByStyle(ToStringStyle.PercentZero), manipulationOffset.ToStringByStyle(ToStringStyle.PercentOne, ToStringNumberSense.Offset)));

        // Melee Weapon DPS
        float dpsOffset = 0f;
        if (meleeDps != null)
        {
            dpsOffset = Remap(8f, 50f, 0f, 0.4f, meleeDps.Value);
            if (dpsOffset != 0)
            {
                str?.AppendLine("AM.Stats.WeaponDPSCoef".Translate(meleeDps.Value.ToStringByStyle(ToStringStyle.FloatOne), dpsOffset.ToStringByStyle(ToStringStyle.PercentOne, ToStringNumberSense.Offset)));
            }
        }

        // Weapon Damage Type (capacity)
        if (capMod != null)
            str?.AppendLine("AM.Stats.WeaponDamageTypeOffset".Translate(capMod.LabelCap, capOffset.ToStringByStyle(ToStringStyle.PercentOne, ToStringNumberSense.Offset)));

        // Friendly bonus.
        float friendBonus = 0f;
        if ((pawn.IsColonist || pawn.IsSlaveOfColony) && Core.Settings.FriendlyPawnLethalityBonus != 0f)
        {
            friendBonus = Core.Settings.FriendlyPawnLethalityBonus;
            str?.AppendLine("AM.Stats.FriendlyPawnBonus".Translate(friendBonus.ToStringByStyle(ToStringStyle.PercentZero, ToStringNumberSense.Offset)));
        }

        // Global settings coef.
        float gCoef = Core.Settings.ExecutionLethalityModifier;
        if (Math.Abs(gCoef - 1f) > 0.001f)
        {
            string name = "AM.Stats.Lethality".Trs();
            str?.AppendLine("AM.Stats.GlobalCoef".Translate(name, gCoef.ToStringByStyle(ToStringStyle.PercentZero, ToStringNumberSense.Factor)));
        }

        float final = Mathf.Max(0f, gCoef * (baseline + meleeSkillOffset + consciousnessOffset + manipulationOffset + dpsOffset + capOffset + friendBonus));
        return final;
    }

    private float Remap(float a, float b, float c, float d, float value)
    {
        return Mathf.Lerp(c, d, Mathf.InverseLerp(a, b, value));
    }
}
