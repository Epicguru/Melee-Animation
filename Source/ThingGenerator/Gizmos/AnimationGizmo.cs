using AAM.Grappling;
using AAM.Reqs;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AAM.PawnData;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace AAM.Gizmos;

public class AnimationGizmo : Gizmo
{
    private readonly Pawn pawn;
    private readonly PawnMeleeData data;
    private readonly List<Pawn> pawns = new List<Pawn>();

    public AnimationGizmo(Pawn pawn)
    {
        this.pawn = pawn;
        this.data = Current.Game.GetComponent<GameComp>().GetOrCreateData(pawn); // TODO optimize.
    }

    public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
    {
        var butRect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);

        Text.Font = GameFont.Tiny;
        MouseoverSounds.DoRegion(butRect, SoundDefOf.Mouseover_Command);

        Widgets.DrawBoxSolidWithOutline(butRect, new Color32(21, 25, 29, 255), Color.white * 0.75f);

        DrawAutoExecute(butRect.LeftHalf().TopHalf());
        DrawAutoGrapple(butRect.LeftHalf().BottomHalf());

        return default;
    }

    public override bool GroupsWith(Gizmo other)
    {
        return other is AnimationGizmo g && g != this;
    }

    public override void MergeWith(Gizmo raw)
    {
        if (raw is not AnimationGizmo other)
            return;

        pawns.Add(other.pawn);
    }

    private void DrawIcon(Rect rect, Texture icon, Color color)
    {
        GUI.DrawTexture(rect, Content.IconBG);

        GUI.color = color;
        GUI.DrawTexture(rect, icon);
        GUI.color = Color.white;

        Widgets.DrawHighlightIfMouseover(rect);
    }

    private void DrawAutoExecute(Rect rect)
    {
        bool multi = pawns.Count > 0;
        bool mixed = false;
        if (multi)
        {
            var mode = data.AutoExecute;
            foreach (var other in pawns)
            {
                if (other.GetMeleeData().AutoExecute != mode)
                    mixed = true;
            }
        }

        AutoOption selected = data.AutoExecute;
        Color iconColor = selected switch
        {
            AutoOption.Default  => Color.grey,
            AutoOption.Enabled  => Color.green,
            AutoOption.Disabled => Color.red,
            _ => Color.magenta
        };
        if (mixed)
            iconColor = Color.yellow;

        DrawIcon(rect, Content.IconExecute, iconColor);

        string sThis = multi ? "these" : "this";
        string sPawns = multi ? $"{pawns.Count + 1} pawns" : "pawn";
        string sHas = multi ? "have" : "has";
        var pawnCount = new NamedArgument(pawns.Count + 1, "PawnCount");

        string tooltip;
        if (mixed)
        {
            // pawns.Count + 1
            tooltip = "AAM.Gizmo.DifferingReset".Trs(pawnCount, new NamedArgument(data.AutoExecute, "Mode"));
        }
        else
        {
            string autoExecFromSettings = Core.Settings.AutoExecute ? "AAM.Gizmo.Enabled".Trs() : "AAM.Gizmo.Disabled".Trs();
            string autoExecFromSettingsColor = Core.Settings.AutoExecute ? "green" : "red";
            bool resolved = selected switch
            {
                AutoOption.Default => Core.Settings.AutoExecute,
                AutoOption.Enabled => true,
                AutoOption.Disabled => false,
                _ => throw new ArgumentOutOfRangeException()
            };
            string plural = multi ? "Plural" : "";
            string explanation = resolved
                ? $"AAM.Gizmo.Execute.ExplainResolved{plural}".Trs(pawnCount)
                : $"AAM.Gizmo.Execute.Explain{plural}".Trs(pawnCount);

            string mode = $"<color={autoExecFromSettingsColor}><b>{autoExecFromSettings}</b></color>";
            var modeArg = new NamedArgument(mode, "Mode");
            var explainationArg = new NamedArgument(explanation, "Explanation");

            tooltip = selected switch
            {
                AutoOption.Default  => $"AAM.Gizmo.Execute.Mode.Default{plural}".Trs(pawnCount, explainationArg, modeArg),
                AutoOption.Enabled  => $"{sThis.CapitalizeFirst()} {sPawns} {sHas} auto-execute <color=green><b>enabled</b></color>.\n\n{explanation}",
                AutoOption.Disabled => $"{sThis.CapitalizeFirst()} {sPawns} {sHas} auto-execute <color=red><b>disabled</b></color>.\n\n{explanation}",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        TooltipHandler.TipRegion(rect, tooltip);

        if (Widgets.ButtonInvisible(rect))
        {
            if (multi)
            {
                if(!mixed)
                    data.AutoExecute = (AutoOption)(((int)data.AutoExecute + 1) % 3);

                // Set all to first pawn's mode.
                foreach (var pawn in pawns)
                    pawn.GetMeleeData().AutoExecute = data.AutoExecute;

            }
            else
            {
                data.AutoExecute = (AutoOption) (((int) data.AutoExecute + 1) % 3);
            }
        }
    }

    private void DrawAutoGrapple(Rect rect)
    {
        bool multi = pawns.Count > 0;
        bool mixed = false;
        if (multi)
        {
            var mode = data.AutoGrapple;
            foreach (var other in pawns)
            {
                if (other.GetMeleeData().AutoGrapple != mode)
                    mixed = true;
            }
        }

        AutoOption selected = data.AutoGrapple;
        Color iconColor = selected switch
        {
            AutoOption.Default => Color.grey,
            AutoOption.Enabled => Color.green,
            AutoOption.Disabled => Color.red,
            _ => Color.magenta
        };
        if (mixed)
            iconColor = Color.yellow;

        DrawIcon(rect, Content.IconGrapple, iconColor);

        string sThis = multi ? "these" : "this";
        string sPawns = multi ? $"{pawns.Count + 1} pawns" : "pawn";
        string sIs = multi ? $"are" : "is";
        string sHas = multi ? "have" : "has";

        string tooltip;
        if (mixed)
        {
            tooltip = $"These {pawns.Count + 1} pawns have differing auto-lasso settings. Click to set them all to '{data.AutoGrapple}'.";
        }
        else
        {
            string autoExecFromSettings = Core.Settings.AutoGrapple ? "enabled" : "disabled";
            string autoExecFromSettingsColor = Core.Settings.AutoGrapple ? "green" : "red";
            bool resolved = selected switch
            {
                AutoOption.Default => Core.Settings.AutoGrapple,
                AutoOption.Enabled => true,
                AutoOption.Disabled => false,
                _ => throw new ArgumentOutOfRangeException()
            };
            string explanation = resolved
                ? $"This means that {sThis} {sPawns} will automatically pull enemies into melee range using an equipped lasso, whenever they can."
                : $"This means that {sThis} {sPawns} will <b>NOT</b> automatically lasso enemies.";

            tooltip = selected switch
            {
                AutoOption.Default  => $"{sThis.CapitalizeFirst()} {sPawns} {sIs} using the <b>default</b> auto-lasso setting, which is <color={autoExecFromSettingsColor}><b>{autoExecFromSettings}</b></color>.\n\n{explanation}",
                AutoOption.Enabled  => $"{sThis.CapitalizeFirst()} {sPawns} {sHas} auto-lasso <color=green><b>enabled</b></color>.\n\n{explanation}",
                AutoOption.Disabled => $"{sThis.CapitalizeFirst()} {sPawns} {sHas} auto-lasso <color=red><b>disabled</b></color>.\n\n{explanation}",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        TooltipHandler.TipRegion(rect, tooltip);

        if (Widgets.ButtonInvisible(rect))
        {
            if (multi)
            {
                if (!mixed)
                    data.AutoGrapple = (AutoOption)(((int)data.AutoGrapple + 1) % 3);

                // Set all to first pawn's mode.
                foreach (var pawn in pawns)
                    pawn.GetMeleeData().AutoGrapple = data.AutoGrapple;

            }
            else
            {
                data.AutoGrapple = (AutoOption)(((int)data.AutoGrapple + 1) % 3);
            }
        }
    }

    private void DrawInfo(Rect rect)
    {
        DrawIcon(rect, Content.IconInfo, Color.cyan);

        var weapon = pawn.GetFirstMeleeWeapon();
        // yes the " 1" is intentional: Rimworld doesn't account for RichText in text size calculation.
        string msg = $"Weapon: {(weapon == null ? "<i>None</i>" : weapon.LabelCap)}";

        TooltipHandler.TipRegion(rect, msg);
    }

    private void DrawSkill(Rect rect)
    {
        DrawIcon(rect, Content.IconSkill, Color.grey);

        TooltipHandler.TipRegion(rect, "<b>Skills:</b>\n\nThis weapon has no special skills available.\n\n<color=yellow>(Not implemented yet!)</color>");
    }

    public override float GetWidth(float maxWidth)
    {
        float target = pawns.Count == 0 ? 180 : 75;
        return Mathf.Min(target, maxWidth);
    }
}