using AM.PawnData;
using RimWorld;
using System;
using UnityEngine;
using Verse;

namespace AM.ColumnWorkers;

public abstract class PawnColumnWorker_Base : PawnColumnWorker
{
    private const int TOP_PADDING = 3;
    private const int WIDTH = 24;

    protected abstract string MakeTooltip(PawnMeleeData data);

    protected abstract Texture2D Icon { get; }

    public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
    {
        if (!pawn.RaceProps.Humanlike || !pawn.RaceProps.ToolUser)
            return;

        var data = pawn.GetMeleeData();
        var c = GetIconColor(pawn, data);

        var uv = new Rect(0, 0, 1, 1);
        GUI.color = c;
        Widgets.DrawTexturePart(rect, uv, Icon);
        GUI.color = Color.white;
        if (Widgets.ButtonInvisible(rect))
        {
            GetOptionRef(data) = (AutoOption)(((int)GetOptionRef(data) + 1) % 3);
        }
        TooltipHandler.TipRegion(rect, MakeTooltip(data));
    }

    protected virtual Color GetIconColor(Pawn pawn, PawnMeleeData data)
    {
        return GetOptionRef(data) switch
        {
            AutoOption.Default => Color.white,
            AutoOption.Enabled => Color.green,
            AutoOption.Disabled => Color.red,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public override int GetMinCellHeight(Pawn pawn)
    {
        return Mathf.Max(base.GetMinCellHeight(pawn), Mathf.CeilToInt(WIDTH) + TOP_PADDING);
    }

    public override int GetMinWidth(PawnTable table)
    {
        return Mathf.Max(base.GetMinWidth(table), WIDTH);
    }

    public override int GetMaxWidth(PawnTable table)
    {
        return Mathf.Min(base.GetMaxWidth(table), this.GetMinWidth(table));
    }

    public override int Compare(Pawn a, Pawn b)
    {
        return this.GetValueToCompare(a).CompareTo(this.GetValueToCompare(b));
    }

    private int GetValueToCompare(Pawn pawn)
    {
        var meleeData = pawn.GetMeleeData();
        return (int)GetOptionRef(meleeData);
    }

    protected abstract ref AutoOption GetOptionRef(PawnMeleeData data);
}
