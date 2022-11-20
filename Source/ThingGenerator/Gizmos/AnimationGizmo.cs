using AAM.Data;
using AAM.Grappling;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace AAM.Gizmos
{
    public class AnimationGizmo : Gizmo
    {
        private readonly Pawn pawn;
        private readonly PawnMeleeData data;
        private readonly List<Pawn> pawns = new();

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
            string sIs = multi ? $"are" : "is";
            string sHas = multi ? "have" : "has";
            var pawnCount = new NamedArgument(pawns.Count + 1, "PawnCount");

            string tooltip;
            if (mixed)
            {
                // pawns.Count + 1
                tooltip = $"AAM.Gizmo.DifferingReset".Trs(pawnCount,
                                                          new NamedArgument(data.AutoExecute, "Mode"));
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

            float cooldown = pawn.GetStatValue(AAM_DefOf.AAM_ExecutionCooldown);

            bool isOffCooldown = data.IsExecutionOffCooldown(cooldown);
            if (!isOffCooldown)
            {
                float pct = data.GetExecuteCooldownPct(cooldown);
                Widgets.FillableBar(rect.BottomPartPixels(14).ExpandedBy(-4, -3), pct);
            }

            Widgets.DrawHighlightIfMouseover(rect);
            if (Widgets.ButtonInvisible(rect) && !Find.Targeter.IsTargeting)
            {
                if(!isOffCooldown)
                {
                    string name = pawn.Name.ToStringShort;
                    Messages.Message($"{name}'s execution is on cooldown!", MessageTypeDefOf.RejectInput, false);
                }
                else if (pawn.GetFirstMeleeWeapon() == null)
                {
                    string name = pawn.Name.ToStringShort;
                    Messages.Message($"{name} cannot execute anyone without a melee weapon!", MessageTypeDefOf.RejectInput, false);
                }
                else
                {
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
                    var args = new TargetingParameters()
                    {
                        canTargetSelf = false,
                        canTargetBuildings = false,
                        validator = LassoTargetValidator
                    };
                    Find.Targeter.BeginTargeting(args, OnSelectedExecutionTarget, null, null, pawn);
                }
                
            }

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, "Execute Target...");
            Text.Anchor = TextAnchor.UpperLeft;

            TooltipHandler.TipRegion(rect, "Use melee weapon to stylishly kill an enemy.\n\nWill use a lasso to pull the victim into striking distance, if possible.\nOtherwise, this pawn will walk towards the target to perform the execution.");
        }

        private void DrawLassoTarget(Rect rect)
        {
            GUI.DrawTexture(rect, Content.IconLongBG);

            float cooldown = pawn.GetStatValue(AAM_DefOf.AAM_GrappleCooldown);

            bool isOffCooldown = data.IsGrappleOffCooldown(cooldown);
            if (!isOffCooldown)
            {
                float pct = data.GetGrappleCooldownPct(cooldown);
                Widgets.FillableBar(rect.BottomPartPixels(14).ExpandedBy(-4, -3), pct);
            }

            Widgets.DrawHighlightIfMouseover(rect);

            if (Widgets.ButtonInvisible(rect) && !Find.Targeter.IsTargeting)
            {
                if(!GrabUtility.CanStartGrapple(pawn, out string reason))
                {
                    Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                }
                else
                {
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
                    var args = new TargetingParameters()
                    {
                        canTargetSelf = false,
                        canTargetBuildings = false,
                        validator = LassoTargetValidator
                    };
                    Find.Targeter.BeginTargeting(args, OnSelectedLassoTarget, null, null, pawn);
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

        private void OnSelectedLassoTarget(LocalTargetInfo info)
        {
            if (info.Thing is not Pawn target || target.Dead)
                return;

            if (target == pawn)
                return;

            var targetPos = target.Position;

            string lastReason = $"No valid position to drag {target.NameShortColored} to.";

            foreach (var pos in GrabUtility.GetIdealGrappleSpots(pawn, target, false))
            {
                if (targetPos == pos)
                    return; // Yes, return, not continue. If the target is already in a good spot, there is nothing to do.

                if (GrabUtility.CanStartGrapple(pawn, target, pos, out lastReason))
                {
                    if (JobDriver_GrapplePawn.GiveJob(pawn, target, pos, true))
                        data.TimeSinceGrappled = 0;
                    return;
                }
            }

            Messages.Message($"Cannot lasso target: {lastReason}", MessageTypeDefOf.RejectInput, false);
        }

        private void OnSelectedExecutionTarget(LocalTargetInfo info)
        {
            // TODO walk to execute.

            if (info.Thing is not Pawn target || target.Dead)
                return;
            if (target == pawn)
                return;

            float grappleCooldown = pawn.GetStatValue(AAM_DefOf.AAM_GrappleCooldown);
            var lasso = pawn.TryGetLasso();

            bool canGrapple = lasso != null && data.IsGrappleOffCooldown(grappleCooldown);
            var targetPos = target.Position;
            string lastReason = canGrapple ? $"No valid position to drag {target.NameShortColored} to." : lasso == null ?  "No lasso is equipped, so the target cannot be dragged in for an execution." : "Lasso is on cooldown, so the target cannot be dragged in for an execution.";

            // TODO start params need to change based on space available.
            var anim = AnimDef.GetExecutionAnimationsForWeapon(pawn.GetFirstMeleeWeapon().def).RandomElementByWeightWithFallback(a => a.Probability);
            if (anim == null)
            {
                Messages.Message("Cannot execute target: No way to execute! (no animations can be played, perhaps because of the weapon or because of a lack of space)", MessageTypeDefOf.RejectInput, false);
                return;
            }

            var startParams = new AnimationStartParameters(anim, pawn, target)
            {
                ExecutionOutcome = ExecutionOutcome.Kill
            };

            foreach (var pos in GrabUtility.GetIdealGrappleSpots(pawn, target, true))
            {
                // Configure horizontal flip...
                startParams.FlipX = pos.x < pawn.Position.x;

                if (pos == targetPos)
                {
                    // Start immediately then exit.
                    if (startParams.TryTrigger())
                        data.TimeSinceExecuted = 0;

                    return;
                }

                // Grapple, then execute.
                if (canGrapple && GrabUtility.CanStartGrapple(pawn, target, pos, out lastReason))
                {

                    if (JobDriver_GrapplePawn.GiveJob(pawn, target, pos, true, startParams))
                        data.TimeSinceGrappled = 0;
                    return;
                }
            }

            Messages.Message($"Cannot execute target: {lastReason}", MessageTypeDefOf.RejectInput, false);
        }

        public override float GetWidth(float maxWidth)
        {
            float target = pawns.Count == 0 ? 180 : 75;
            return Mathf.Min(target, maxWidth);
        }
    }
}
