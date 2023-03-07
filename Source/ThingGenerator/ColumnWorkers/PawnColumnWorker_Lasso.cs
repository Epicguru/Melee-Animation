using System;
using AM.PawnData;
using UnityEngine;
using Verse;

namespace AM.ColumnWorkers;

public class PawnColumnWorker_Lasso : PawnColumnWorker_Base
{
    protected override Texture2D Icon => Content.IconGrapple;

    protected override string MakeTooltip(PawnMeleeData data)
    {
        var tip = data.AutoGrapple switch
        {
            AutoOption.Default => $"Auto-Lasso is default, which means {(Core.Settings.AutoGrapple ? "Enabled" : "Disabled")} due to mod settings",
            AutoOption.Enabled => "Auto-Lasso is Enabled.",
            AutoOption.Disabled => "Auto-Lasso is Disabled.",
            _ => throw new ArgumentOutOfRangeException()
        };

        bool hasLasso = data.Pawn?.TryGetLasso() != null;
        if (!hasLasso)
        {
            tip += $"\n{data.Pawn?.NameShortColored} does not have a lasso equipped.";
        }

        return tip;
    }

    protected override ref AutoOption GetOptionRef(PawnMeleeData data) => ref data.AutoGrapple;

    protected override Color GetIconColor(Pawn pawn, PawnMeleeData data)
    {
        bool hasLasso = pawn.TryGetLasso() != null;
        if (!hasLasso)
            return Color.yellow;
        return base.GetIconColor(pawn, data);
    }
}
