using AAM.Data;
using AAM.Grappling;
using AAM.Reqs;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    private static readonly HashSet<AnimDef> except = new HashSet<AnimDef>();

    private bool forceDontUseLasso;

    public AnimationGizmo(Pawn pawn)
    {
        this.pawn = pawn;
        this.data = Current.Game.GetComponent<GameComp>().GetOrCreateData(pawn); // TODO optimize.
    }

    public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
    {
        if(pawns.Count == 0)
            return DrawSingle(topLeft, maxWidth, parms);

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

    private GizmoResult DrawSingle(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
    {
        var butRect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
        try
        {
            Text.Font = GameFont.Tiny;

            // Mouseover and highlight.
            MouseoverSounds.DoRegion(butRect, SoundDefOf.Mouseover_Command);

            bool mouseOver = Mouse.IsOver(butRect);

            Widgets.DrawBoxSolidWithOutline(butRect, new Color32(21, 25, 29, 255), Color.white * 0.75f);

            Rect left = butRect.ExpandedBy(-2).LeftPartPixels(34);
            Rect tl = left.TopPartPixels(34);
            Rect bl = left.BottomPartPixels(34);

            Rect right = butRect.ExpandedBy(-2).RightPartPixels(34);
            Rect tr = right.TopPartPixels(34);
            Rect br = right.BottomPartPixels(34);

            Rect middle = butRect.ExpandedBy(-2).ExpandedBy(-36, 0);

            Rect tm = middle.TopPartPixels(34).ExpandedBy(-2, 0);
            Rect bm = middle.BottomPartPixels(34).ExpandedBy(-2, 0);

            DrawAutoExecute(tl);
            DrawAutoGrapple(bl);
            DrawInfo(tr);
            DrawSkill(br);
            DrawExecuteTarget(tm);
            DrawLassoTarget(bm);

            string pawnName = null;
            if (Find.Selector.SelectedPawns.Count > 1)
                pawnName = pawn.NameShortColored;
            string labelCap = $"{"AAM.Gizmo.Title".Trs()}{(pawnName != null ? $"({pawnName})" : "")}";

            if (!labelCap.NullOrEmpty())
            {
                Text.Font = GameFont.Tiny;
                float num = Text.CalcHeight(labelCap, butRect.width);
                Rect rect2 = new(butRect.x, butRect.yMax - num + 12f, butRect.width, num);
                GUI.DrawTexture(rect2, TexUI.GrayTextBG);
                Text.Anchor = TextAnchor.UpperCenter;
                Widgets.Label(rect2, labelCap);
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }

            TooltipHandler.TipRegion(butRect, string.Join(", ", pawns));

            GUI.color = Color.white;

            bool HasLos(IntVec3 c)
            {
                return GenSight.LineOfSight(pawn.Position, c, pawn.Map);
            }

            // Note: despite best efforts, may still display radius for other custom targeting operations when this pawn is selected.
            if (Event.current.type == EventType.Repaint && Find.Targeter.IsTargeting && Find.Targeter.IsPawnTargeting(pawn) && Find.Targeter.targetingSource == null)
            {
                if (pawn.TryGetLasso() != null)
                {
                    pawn.GetAnimManager().AddPostDraw(() =>
                    {
                        float radius = pawn.GetStatValue(AAM_DefOf.AAM_GrappleRadius);
                        GenDraw.DrawRadiusRing(pawn.Position, radius, Color.yellow, HasLos);
                    });
                }
            }

            var state = mouseOver ? GizmoState.Mouseover : GizmoState.Clear;
            return new GizmoResult(state, null);
        }
        catch (Exception e)
        {
            Text.Font = GameFont.Tiny;
            Widgets.DrawBoxSolid(butRect, Color.black);
            Widgets.Label(butRect, $"<color=red>Exception drawing AdvancedMelee gizmo:\n{e}</color>");
            Text.Font = GameFont.Small;
            Core.Error($"Exception drawing gizmo for {pawn.NameFullColored}!", e);
            return default;
        }
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

    private void DrawExecuteTarget(Rect rect)
    {
        GUI.DrawTexture(rect, Content.IconLongBG);

        bool isOffCooldown = data.IsExecutionOffCooldown();
        if (!isOffCooldown)
        {
            float pct = data.GetExecuteCooldownPct();
            Widgets.FillableBar(rect.BottomPartPixels(14).ExpandedBy(-4, -3), pct);
        }

        Widgets.DrawHighlightIfMouseover(rect);
        if (Widgets.ButtonInvisible(rect) && !Find.Targeter.IsTargeting)
        {
            if(!isOffCooldown)
            {
                string error = "AAM.Gizmo.Execute.OnCooldown".Translate(new NamedArgument(pawn.NameShortColored, "Pawn"));
                Messages.Message(error, MessageTypeDefOf.RejectInput, false);
            }
            else if (pawn.GetFirstMeleeWeapon() == null)
            {
                string name = pawn.Name.ToStringShort;
                Messages.Message($"{name} cannot execute anyone without a melee weapon!", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                forceDontUseLasso = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                var args = new TargetingParameters
                {
                    canTargetSelf = false,
                    canTargetBuildings = false,
                    validator = LassoTargetValidator
                };
                Find.Targeter.BeginTargeting(args, target => OnSelectedExecutionTarget(forceDontUseLasso, pawn, target), null, null, pawn);
            }
                
        }

        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect, "Execute Target...");
        Text.Anchor = TextAnchor.UpperLeft;

        TooltipHandler.TipRegion(rect, "AAM.Gizmo.Execute.Tooltip".Trs());
    }

    private void DrawLassoTarget(Rect rect)
    {
        GUI.DrawTexture(rect, Content.IconLongBG);

        bool isOffCooldown = data.IsGrappleOffCooldown();
        if (!isOffCooldown)
        {
            float pct = data.GetGrappleCooldownPct();
            Widgets.FillableBar(rect.BottomPartPixels(14).ExpandedBy(-4, -3), pct);
        }

        Widgets.DrawHighlightIfMouseover(rect);

        if (Widgets.ButtonInvisible(rect) && !Find.Targeter.IsTargeting)
        {
            if(!GrabUtility.CanStartGrapple(pawn, out string reason, true))
            {
                Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                var args = new TargetingParameters()
                {
                    canTargetSelf = false,
                    canTargetBuildings = false,
                    validator = LassoTargetValidator
                };
                Find.Targeter.BeginTargeting(args, target => OnSelectedLassoTarget(pawn, target), null, null, pawn);
            }
        }

        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect, "Lasso Target...");
        Text.Anchor = TextAnchor.UpperLeft;

        TooltipHandler.TipRegion(rect, "Use a lasso to drag in a pawn.\nYou can lasso enemies or friendlies, even if they are downed.\nLassoing does not harm the target.");
    }

    private bool LassoTargetValidator(TargetInfo info)
    {
        return info.HasThing && info.Thing is Pawn { Dead: false, Spawned: true } p && p != pawn && (Core.Settings.AnimalsCanBeExecuted || !p.RaceProps.Animal);
    }

    public static void OnSelectedLassoTarget(Pawn pawn, LocalTargetInfo info) => OnSelectedLassoTarget(pawn, info, true);

    public static string OnSelectedLassoTarget(Pawn pawn, LocalTargetInfo info, bool performAction)
    {
        if (info.Thing is not Pawn target || target.Dead)
            return "Target is dead or invalid"; // Should never be possible due to targeting.

        if (target == pawn)
            return "Cannot lasso self."; // Should never be possible due to targeting.

        var targetPos = target.Position;
        string lastReason = "AAM.Gizmo.Grapple.CannotLassoDefault".Translate(new NamedArgument(target.NameShortColored, "Target"));

        if (!GrabUtility.CanStartGrapple(pawn, out string reason, true))
        {
            string error = "AAM.Gizmo.Grapple.CannotLassoGeneric".Translate(
                new NamedArgument(pawn.NameShortColored, "Pawn"),
                new NamedArgument(target.NameShortColored, "Target"),
                new NamedArgument(reason.CapitalizeFirst(), "Reason"));

            if (performAction)
                Messages.Message(error, MessageTypeDefOf.RejectInput, false);
            return error;
        }

        // Enumerates free cells in the 3x3 area around the pawn.
        // Prioritizing the cells closest to the target (minimize drag distance).
        foreach (var pos in GrabUtility.GetIdealGrappleSpots(pawn, target, false))
        {
            if (targetPos == pos)
                return null; // Yes, return, not continue. If the target is already in a good spot, there is nothing to do.

            if (GrabUtility.CanStartGrapple(pawn, target, pos, out lastReason))
            {
                if (performAction)
                {
                    var data = pawn.GetMeleeData();
                    if (JobDriver_GrapplePawn.GiveJob(pawn, target, pos, true))
                        data.TimeSinceGrappled = 0;
                }
                return null;
            }
        }

        string error2 = "AAM.Gizmo.Grapple.CannotLassoGeneric".Translate(
            new NamedArgument(pawn.NameShortColored, "Pawn"),
            new NamedArgument(target.NameShortColored, "Target"),
            new NamedArgument(lastReason.CapitalizeFirst(), "Reason"));
        if (performAction)
            Messages.Message(error2, MessageTypeDefOf.RejectInput, false);
        return error2;
    }

    public static void OnSelectedExecutionTarget(bool forceDontUseLasso, Pawn pawn, LocalTargetInfo info) => OnSelectedExecutionTarget(forceDontUseLasso, pawn, info, true);

    public static string OnSelectedExecutionTarget(bool forceDontUseLasso, Pawn pawn, LocalTargetInfo info, bool performAction)
    {
        if (info.Thing is not Pawn target || target.Dead)
            return "Target is dead or invalid"; // Should not happen due to targeting.

        if (target == pawn)
            return "Cannot execute self!"; // Should not happen due to targeting.

        if (target.Dead || target.Downed)
        {
            string reason = "AAM.Gizmo.Execute.NotDeadOrDowned".Translate(new NamedArgument(target.NameShortColored, "Target"));
            string error = "AAM.Gizmo.Execute.Fail".Translate(new NamedArgument(reason, "Reason"));
            if (performAction)
                Messages.Message(error, MessageTypeDefOf.RejectInput, false);
            return error;
        }

        // Cooldown.
        if (!pawn.GetMeleeData().IsExecutionOffCooldown())
        {
            string error = "AAM.Gizmo.Execute.OnCooldown".Translate(new NamedArgument(pawn.NameShortColored, "Pawn"));
            if (performAction)
                Messages.Message(error, MessageTypeDefOf.RejectInput, false);
            return error;
        }

        if (!forceDontUseLasso)
            forceDontUseLasso = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        bool allowFriendlies = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (Core.Settings.WarnOfFriendlyExecution && !allowFriendlies && !target.AnimalOrWildMan() && !target.HostileTo(Faction.OfPlayerSilentFail))
        {
            string error = "AAM.Gizmo.Grapple.CtrlForFriendlies".Trs();
            if (performAction)
                Messages.Message(error, MessageTypeDefOf.RejectInput, false);
            return error;
        }

        var lasso = pawn.TryGetLasso();
        bool canGrapple = !forceDontUseLasso && lasso != null && GrabUtility.CanStartGrapple(pawn, out _);
        IntVec3 targetPos = target.Position;

        // If grappling is enabled, but pawn is out of reach, send warning message
        // letting the player know they can shift+click to send the pawn walking.
        if (canGrapple)
        {
            float currDistanceSqr = (pawn.Position - targetPos).LengthHorizontalSquared;
            float maxDistance = pawn.GetStatValue(AAM_DefOf.AAM_GrappleRadius);
            if (maxDistance * maxDistance < currDistanceSqr)
            {
                string error = "AAM.Gizmo.Grapple.OutOfRange".Translate(new NamedArgument(pawn.NameShortColored, "Pawn"));
                Messages.Message(error, MessageTypeDefOf.RejectInput, false);
                return error;
            }
        }

        var weaponDef = pawn.GetFirstMeleeWeapon().def;
        var possibilities = AnimDef.GetExecutionAnimationsForPawnAndWeapon(pawn, weaponDef);
        ulong occupiedMask = SpaceChecker.MakeOccupiedMask(pawn.Map, pawn.Position, out _);
            
        if (!possibilities.Any())
        {
            string reason = "AAM.Gizmo.Error.NoAnimations".Translate(
                new NamedArgument(weaponDef.LabelCap, "Weapon"),
                new NamedArgument(pawn.LabelShortCap, "Pawn"));

            string error = "AAM.Gizmo.Execute.Fail".Translate(new NamedArgument(reason, "Reason"));
                
            if (performAction)
                Messages.Message(error, MessageTypeDefOf.RejectInput, false);
            return error;
        }

        // Step 1: Check if the pawn is in the left or right position.
        if (targetPos == pawn.Position - new IntVec3(1, 0, 0) ||
            targetPos == pawn.Position + new IntVec3(1, 0, 0))
        {
            // If the target is in one of these two spots, it may be a candidate for a simple immediate execution...
            bool flipX = targetPos.x < pawn.Position.x;

            // Check space...
            except.Clear();
            while (true)
            {
                // Pick random anim, weighted.
                var anim = possibilities.RandomElementByWeightExcept(d => d.Probability, except);
                if (anim == null)
                    break;

                except.Add(anim);

                // Do we have space for this animation?
                ulong animMask = flipX ? anim.FlipClearMask : anim.ClearMask;
                ulong result = animMask & occupiedMask; // The result should be 0.

                if (result == 0)
                {
                    if (!performAction)
                        return null;

                    // Can do the animation!
                    var startArgs = new AnimationStartParameters(anim, pawn, target)
                    {
                        ExecutionOutcome = OutcomeUtility.GenerateRandomOutcome(pawn, target),
                        FlipX = flipX,
                    };

                    if (startArgs.TryTrigger())
                    {
                        // Set execute cooldown.
                        pawn.GetMeleeData().TimeSinceExecuted = 0;
                    }
                    else
                    {
                        Core.Error($"Failed to start execution animation (instant) for pawn {pawn} on {target}.");
                    }

                    return null;
                }
            }

            // At this point, the pawn is to the immediate left or right but none of the animations could be played due to space constraints.
            // Therefore, exit.
            string error = "AAM.Gizmo.Error.NoSpace".Translate();
            if (performAction)
                Messages.Message(error, MessageTypeDefOf.RejectInput, false);
            return error;
        }

        // Step 2: The pawn needs to be grappled in because the target is not to the immediate left or right.
        if (!canGrapple)
        {
            bool canReach = pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly);
            if (!canReach)
            {
                string r = "AAM.Gizmo.Error.CantReach".Translate(new NamedArgument(pawn.NameShortColored, "Pawn"));
                string e = "AAM.Gizmo.Execute.Fail".Translate(new NamedArgument(r, "Reason"));
                if (performAction)
                    Messages.Message(e, MessageTypeDefOf.RejectInput, false);
                return e;
            }
            
            // If not performing, assume that they can complete the walk job just fine.
            if (!performAction)
                return null;

            // Walk to and execute target.
            // Make walk job and reset all verbs (stop firing, attacking).
            var walkJob = JobMaker.MakeJob(AAM_DefOf.AAM_WalkToExecution, target);

            if (pawn.verbTracker?.AllVerbs != null)
                foreach (var verb in pawn.verbTracker.AllVerbs)
                    verb.Reset();


            if (pawn.equipment?.AllEquipmentVerbs != null)
                foreach (var verb in pawn.equipment.AllEquipmentVerbs)
                    verb.Reset();

            // Start walking.
            pawn.jobs.StartJob(walkJob, JobCondition.InterruptForced);

            if (pawn.CurJobDef == AAM_DefOf.AAM_WalkToExecution)
                return null;

            Core.Error($"CRITICAL ERROR: Failed to force interrupt {pawn}'s job with execution goto job. Likely a mod conflict or invalid start parameters.");
            string reason = "AAM.Gizmo.Error.NoLasso".Translate(
                new NamedArgument(pawn.NameShortColored, "Pawn"),
                new NamedArgument(target.NameShortColored, "Target"));
            string error = "AAM.Gizmo.Execute.Fail".Translate(new NamedArgument(reason, "Reason"));
            Messages.Message(error, MessageTypeDefOf.RejectInput, false);

            return error;
        }

        // Try left and right position.
        string lastReasonNoGrapple = null;
        foreach (var cell in GrabUtility.GetIdealGrappleSpots(pawn, target, true))
        {
            bool flipX = cell.x < pawn.Position.x;

            // Check line of sight and other details.
            if (!GrabUtility.CanStartGrapple(pawn, target, cell, out lastReasonNoGrapple))
                continue;

            except.Clear();
            while (true)
            {
                // Pick random anim, weighted.
                var anim = possibilities.RandomElementByWeightExcept(d => d.Probability, except);
                if (anim == null)
                    break;

                except.Add(anim);

                // Do we have space for this animation?
                ulong animMask = flipX ? anim.FlipClearMask : anim.ClearMask;
                ulong result = animMask & occupiedMask; // The result should be 0.

                if (result == 0)
                {
                    // There is space for the animation, and line of sight for the grapple.
                    // So good to go.
                    if (!performAction)
                        return null;

                    var startParams = new AnimationStartParameters
                    {
                        Animation = anim,
                        ExecutionOutcome = OutcomeUtility.GenerateRandomOutcome(pawn, target),
                        FlipX = flipX,
                        MainPawn = pawn,
                        SecondPawn = target,
                        ExtraPawns = null,
                        Map = pawn.Map,
                        RootTransform = pawn.MakeAnimationMatrix(),
                    };

                    // Give grapple job and pass in parameters which trigger the execution animation.
                    if (JobDriver_GrapplePawn.GiveJob(pawn, target, cell, true, startParams))
                        pawn.GetMeleeData().TimeSinceGrappled = 0;
                    else
                        Core.Error($"Failed to give grapple job to {pawn}.");

                    return null;
                }
            }
        }

        // Failed to lasso, or failed to find any space for any animation.
        if (lastReasonNoGrapple != null)
        {
            // Cannot lasso target.
            string reason = "AAM.Gizmo.Error.CannotLasso".Translate(
                new NamedArgument(pawn.NameShortColored, "Pawn"),
                new NamedArgument(target.NameShortColored, "Target"),
                new NamedArgument(lastReasonNoGrapple, "Reason"));

            string error = "AAM.Gizmo.Execute.Fail".Translate(new NamedArgument(reason, "Reason"));
            if (performAction)
                Messages.Message(error, MessageTypeDefOf.RejectInput, false);
            return error;
        }
        else
        {
            // No Space.
            string error = "AAM.Gizmo.Error.NoSpace".Translate();
            Messages.Message(error, MessageTypeDefOf.RejectInput, false);
            return error;
        }
    }

    public static string OnSelectedDuelTarget(bool forceDontUseLasso, Pawn pawn, LocalTargetInfo info, bool performAction)
    {
        if (info.Thing is not Pawn target || target.Dead)
            return "Target is dead or invalid"; // Should not happen due to targeting.

        if (target == pawn)
            return "Cannot duel self!"; // Should not happen due to targeting.

        // Check for missing weapons.
        var mainWeapon = pawn.GetFirstMeleeWeapon();
        var targetWeapon = target.GetFirstMeleeWeapon();
        if (mainWeapon == null || targetWeapon == null)
        {
            Pawn missing = mainWeapon == null ? pawn : target;
            string reason = "AAM.Gizmo.Error.NoValidWeapon".Translate(new NamedArgument(missing.NameShortColored, "Pawn"));
            string error  = "AAM.Gizmo.Duel.Fail".Translate(new NamedArgument(reason, "Reason"));

            if (performAction)
                Messages.Message(error, MessageTypeDefOf.RejectInput, false);
            return error;
        }

        ReqInput mainInput   = new ReqInput(mainWeapon.def);
        ReqInput secondInput = new ReqInput(targetWeapon.def);

        bool CanDuel(AnimDef def)
        {
            if (def.weaponFilterSecond == null)
                return def.weaponFilter.Evaluate(mainInput) && def.weaponFilter.Evaluate(secondInput);

            return (def.weaponFilter.Evaluate(mainInput) && def.weaponFilterSecond.Evaluate(secondInput)) ||
                   (def.weaponFilter.Evaluate(secondInput) && def.weaponFilterSecond.Evaluate(mainInput));
        }

        // Get all possible duel animations.
        var compatible = AnimDef.GetDefsOfType(AnimType.Duel).Where(CanDuel);
        var selected = compatible.RandomElementByWeightWithFallback(d => d.Probability);
        if (selected == null)
        {
            string reason = "AAM.Gizmo.NoDuelAnim".Translate(
                new NamedArgument(pawn.NameShortColored, "Pawn"),
                new NamedArgument(target.NameShortColored, "Target"));
            string error = "AAM.Gizmo.Duel.Fail".Translate(new NamedArgument(reason, "Reason"));

            if (performAction)
                Messages.Message(error, MessageTypeDefOf.RejectInput, false);
            return error;
        }

        // Are the pawns standing next to each other?
        int delta = pawn.Position.x - target.Position.x;
        if (pawn.Position.z == target.Position.z && Mathf.Abs(delta) == 1)
        {
            // They are in the correct positions, start immediately!
            // TODO start immediately.
            return null;
        }

        IEnumerable<IntVec3> EnumerateGrappleSpots()
        {
            Map map = pawn.Map;
            int w = map.info.Size.x;
            int h = map.info.Size.z;

            int dir = pawn.Position.x < target.Position.x ? 1 : -1;

            if (SpaceChecker.IsValidPawnPosFast(map, w, h, pawn.Position + new IntVec3(dir, 0, 0)))
                yield return pawn.Position + new IntVec3(dir, 0, 0);

            if (SpaceChecker.IsValidPawnPosFast(map, w, h, pawn.Position - new IntVec3(dir, 0, 0)))
                yield return pawn.Position - new IntVec3(dir, 0, 0);
        }

        // Check if the lasso can be used.
        bool canGrapple = !forceDontUseLasso;
        IntVec3 grappleTargetPos = default;
        if (canGrapple)
        {
            bool foundGrappleSpot = false;
            foreach (var spot in EnumerateGrappleSpots())
            {
                if (GrabUtility.CanStartGrapple(pawn, target, spot, out _, true))
                {
                    grappleTargetPos = spot;
                    foundGrappleSpot = true;
                    break;
                }
            }

            if (!foundGrappleSpot)
                canGrapple = false;
        }

        // Forced to walk to target.
        if (!canGrapple)
        {
            // Quick pathfinding check.
            bool canReach = pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly);
            if (!canReach)
            {
                string r = "AAM.Gizmo.Error.CantReach".Translate(new NamedArgument(pawn.NameShortColored, "Pawn"));
                string e = "AAM.Gizmo.Duel.Fail".Translate(new NamedArgument(r, "Reason"));
                if (performAction)
                    Messages.Message(e, MessageTypeDefOf.RejectInput, false);
                return e;
            }

            // If not performing, assume that they can complete the walk job just fine.
            if (!performAction)
                return null;

            // Walk to and duel target.
            // Make walk job and reset all verbs (stop firing, attacking).
            var walkJob = JobMaker.MakeJob(AAM_DefOf.AAM_WalkToDuel, target);

            if (pawn.verbTracker?.AllVerbs != null)
                foreach (var verb in pawn.verbTracker.AllVerbs)
                    verb.Reset();


            if (pawn.equipment?.AllEquipmentVerbs != null)
                foreach (var verb in pawn.equipment.AllEquipmentVerbs)
                    verb.Reset();

            // Start walking.
            pawn.jobs.StartJob(walkJob, JobCondition.InterruptForced);

            if (pawn.CurJobDef == AAM_DefOf.AAM_WalkToDuel)
                return null;

            Core.Error($"CRITICAL ERROR: Failed to force interrupt {pawn}'s job with duel goto job. Likely a mod conflict or invalid start parameters.");
            string reason = "AAM.Gizmo.Error.NoLasso".Translate(
                new NamedArgument(pawn.NameShortColored, "Pawn"),
                new NamedArgument(target.NameShortColored, "Target"));
            string error = "AAM.Gizmo.Duel.Fail".Translate(new NamedArgument(reason, "Reason"));
            Messages.Message(error, MessageTypeDefOf.RejectInput, false);

            return error;
        }

        // Pull the target in.
        // TODO important allow duel to be played on either pawn to increase animation variety!
        bool flipX = grappleTargetPos.x < pawn.Position.x;
        var startParams = new AnimationStartParameters
        {
            Animation = selected,
            FlipX = flipX,
            MainPawn = pawn,
            SecondPawn = target,
            ExtraPawns = null,
            Map = pawn.Map,
            RootTransform = pawn.MakeAnimationMatrix(),
        };

        // Give grapple job and pass in parameters which trigger the execution animation.
        if (JobDriver_GrapplePawn.GiveJob(pawn, target, grappleTargetPos, false, startParams))
            pawn.GetMeleeData().TimeSinceGrappled = 0;
        else
            Core.Error($"Failed to give grapple job to {pawn}.");

        return null;
    }

    public override float GetWidth(float maxWidth)
    {
        float target = pawns.Count == 0 ? 180 : 75;
        return Mathf.Min(target, maxWidth);
    }
}