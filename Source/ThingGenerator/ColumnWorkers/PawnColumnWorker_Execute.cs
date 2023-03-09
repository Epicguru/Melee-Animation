using System;
using AM.PawnData;
using UnityEngine;
using Verse;

namespace AM.ColumnWorkers;

public class PawnColumnWorker_Execute : PawnColumnWorker_Base
{
    protected override Texture2D Icon => Content.IconExecute;

    protected override string MakeTooltip(PawnMeleeData data)
    {
        var tip = data.AutoExecute switch
        {
            AutoOption.Default => $"Auto-Execute is set to default, which means {(Core.Settings.AutoExecute ? "Enabled" : "Disabled")} due to mod settings",
            AutoOption.Enabled => "Auto-Execute is Enabled.",
            AutoOption.Disabled => "Auto-Execute is Disabled.",
            _ => throw new ArgumentOutOfRangeException()
        };

        bool hasMelee = data.Pawn?.GetFirstMeleeWeapon() != null;
        if (!hasMelee)
        {
            tip += $"\n{data.Pawn?.NameShortColored} does not have a compatible melee weapon so they will never do execution animations.";
        }

        return tip;
    }

    protected override ref AutoOption GetOptionRef(PawnMeleeData data) => ref data.AutoExecute;

    protected override Color GetIconColor(Pawn pawn, PawnMeleeData data)
    {
        bool hasMelee = pawn.GetFirstMeleeWeapon() != null;
        return !hasMelee ? Color.yellow : base.GetIconColor(pawn, data);
    }
}
