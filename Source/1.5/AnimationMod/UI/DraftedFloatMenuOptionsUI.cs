using System;
using System.Collections.Generic;
using System.Linq;
using AM.Controller;
using AM.Controller.Reports;
using AM.Controller.Requests;
using AM.Grappling;
using AM.Idle;
using AM.Jobs;
using AM.Outcome;
using AM.Reqs;
using AM.UniqueSkills;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AM.UI;

/// <summary>
/// These are the additional options that appear when a drafted pawn with a melee
/// weapon right-clicks on a target pawn.
/// Adds the execute, lasso and unique skill options.
/// </summary>
public static class DraftedFloatMenuOptionsUI
{
    private static readonly ActionController controller = new ActionController();
    private static readonly TargetingParameters targetingArgs = new TargetingParameters()
    {
        canTargetAnimals = true,
        canTargetMechs = true,
        canTargetItems = false,
        canTargetLocations = false,
        canTargetPawns = true,
        canTargetHumans = true,
        canTargetPlants = false,
        canTargetBuildings = false,
        canTargetSelf = false,
        canTargetFires = false,
        canTargetCorpses = false,
        canTargetBloodfeeders = true,
    };
    private static readonly List<FloatMenuOption> tempOptions = new List<FloatMenuOption>();

    public static IEnumerable<FloatMenuOption> GenerateMenuOptions(Vector3 clickPos, Pawn pawn)
    {
        lock (tempOptions)
        {
            tempOptions.Clear();

            try
            {
                tempOptions.AddRange(GenAllOptions(clickPos, pawn));
            }
            catch (Exception e)
            {
                Core.Error($"Float menu UI gen error [{e.Message}]:", e);
            }

            foreach (var op in tempOptions)
                if (op != null)
                    yield return op;

            tempOptions.Clear();
        }
    }

    private static IEnumerable<FloatMenuOption> GenAllOptions(Vector3 clickPos, Pawn pawn)
    {
        if (!pawn.IsColonistPlayerControlled)
            yield break;

        // Downed pawns can't do anything.
        if (pawn.Downed)
            yield break;

        var weapon = pawn.GetFirstMeleeWeapon();
        var lasso = pawn.TryGetLasso();
        var skills = pawn.GetComp<IdleControllerComp>()?.GetSkills();
        bool isFistFighter = pawn.IsCapableOfFistExecutions(out string reason);
        if (weapon == null && lasso == null && !(skills?.Any(s => s?.IsEnabledForPawn(out _) ?? false) ?? false) && !isFistFighter)
            yield break;

        ulong occupiedMask = SpaceChecker.MakeOccupiedMask(pawn.Map, pawn.Position, out uint smallMask);

        var targets = GenUI.TargetsAt(clickPos, targetingArgs, true);
        bool noExecEver = false;

        IEnumerable<FloatMenuOption> GetExecutionAttemptOption(Pawn target)
        {
            // Skip everything if executions are not enabled.
            // This hides the UI and avoids the processing.
            if (!Core.Settings.EnableExecutions)
            {
                noExecEver = true;
                yield break;
            }

            var request = new ExecutionAttemptRequest
            {
                CanUseLasso = lasso != null,
                CanWalk = true,
                EastCell = !occupiedMask.GetBit(1, 0),
                WestCell = !occupiedMask.GetBit(-1, 0),
                Executioner = pawn,
                OccupiedMask = occupiedMask,
                SmallOccupiedMask = smallMask,
                Target = target
            };

            var reports = controller.GetExecutionReport(request);
            foreach (var report in reports)
            {
                if (report is { IsFinal: true, CanExecute: false })
                {
                    noExecEver = true;
                    yield return GetDisabledExecutionOption(report, pawn);
                    break;
                }

                yield return report.CanExecute ? GetEnabledExecuteOption(request, report, pawn, target) : GetDisabledExecutionOption(report, pawn);
            }
        }

        foreach (var t in targets)
        {
            var target = t.Pawn ?? t.Thing as Pawn;
            if (target == null || target.Dead)
                continue;

            // Cannot target self.
            if (target == pawn)
                continue;

            // Unique skills:
            foreach (var op in GenerateSkillOptions(pawn, target, skills))
                yield return op;

            bool isEnemy = target.HostileTo(Faction.OfPlayer);
            
            // Lasso:
            if (lasso != null)
            {
                var request = new GrappleAttemptRequest
                {
                    Grappler = pawn,
                    Target = target,
                    DoNotCheckLasso = true,
                    GrappleSpotPickingBehaviour = isEnemy ? GrappleSpotPickingBehaviour.PreferAdjacent : GrappleSpotPickingBehaviour.Closest,
                    OccupiedMask = smallMask                    
                };
                var grappleReport = controller.GetGrappleReport(request);

                yield return grappleReport.CanGrapple ? GetEnabledLassoOption(request, grappleReport, pawn, target) : GetDisabledLassoOption(grappleReport, pawn);
            }

            if ((target.def.race?.Animal ?? false) && !Core.Settings.AnimalsCanBeExecuted)
                continue;

            if ((weapon != null || isFistFighter) && !noExecEver)
            {
                foreach (var op in GetExecutionAttemptOption(target))
                    if (op != null)
                        yield return op;

                // Skill executions?
                if (skills != null)
                {
                    foreach (var skill in skills)
                    {
                        if (skill == null || skill.Def?.type != SkillType.Execution ||!skill.IsEnabledForPawn(out _))
                            continue;

                        string skillName = skill.Def.label;
                        string reasonCantCast = skill.CanTriggerOn(target);
                        if (reasonCantCast != null)
                        {
                            yield return new FloatMenuOption("AM.Skill.CantCast".Translate(skillName, reasonCantCast), () => { })
                            {
                                Disabled = true
                            };
                            continue;
                        }

                        bool enemy = target.HostileTo(Faction.OfPlayer);
                        var priority = enemy ? MenuOptionPriority.AttackEnemy : MenuOptionPriority.VeryLow;

                        yield return new FloatMenuOption("AM.Skill.Cast".Translate(skillName, target), () =>
                        {
                            if (Core.Settings.WarnOfFriendlyExecution)
                            {
                                enemy = target.HostileTo(Faction.OfPlayer) || (target.def.race?.Animal ?? false);
                                if (!enemy && !Input.GetKey(KeyCode.LeftShift))
                                {
                                    Messages.Message("Tried to cast skill on friendly: hold the Shift key when selecting to confirm!", MessageTypeDefOf.RejectInput, false);
                                    return;
                                }
                            }

                            string msg;
                            if (!skill.IsEnabledForPawn(out _) || skill.CanTriggerOn(target) != null)
                            {
                                msg = "AM.Skill.CantCastGeneric".Translate(skillName, target);
                                Messages.Message(msg, MessageTypeDefOf.RejectInput, false);
                                return;
                            }

                            Core.Log($"Attempting cast of {skillName} on {target} by {pawn}");
                            if (skill.TryTrigger(target))
                            {
                                IEnumerable<AnimDef> OnlyAnimsMethod(UniqueSkillInstance s)
                                {
                                    yield return s.Def.animation;
                                }

                                var request = new ExecutionAttemptRequest
                                {
                                    CanUseLasso = lasso != null,
                                    CanWalk = true,
                                    EastCell = !occupiedMask.GetBit(1, 0),
                                    WestCell = !occupiedMask.GetBit(-1, 0),
                                    Executioner = pawn,
                                    OccupiedMask = occupiedMask,
                                    SmallOccupiedMask = smallMask,
                                    Target = target,
                                    OnlyTheseAnimations = OnlyAnimsMethod(skill)
                                };

                                var reports = controller.GetExecutionReport(request);

                                ExecutionEnabledOnClick(target, pawn, reports.First(), request);
                                return;
                            }

                            msg = "AM.Skill.CantCastGeneric".Translate(skillName, target);
                            Messages.Message(msg, MessageTypeDefOf.RejectInput, false);

                        }, priority, revalidateClickTarget: target);
                    }
                }
            }
        }
    }

    private static IEnumerable<FloatMenuOption> GenerateSkillOptions(Pawn attacker, Pawn target, IReadOnlyList<UniqueSkillInstance> skills)
    {
        if (skills == null || skills.Count == 0)
            yield break;

        foreach (var skill in skills)
        {
            if (skill == null)
                continue;

            if (!skill.IsEnabledForPawn(out _))
                continue;

            // Execution skills are handled elsewhere:
            if (skill.Def.type == SkillType.Execution)
                continue;

            string skillName = skill.Def.label;

            string reasonCantCast = skill.CanTriggerOn(target);
            if (reasonCantCast != null)
            {
                yield return new FloatMenuOption("AM.Skill.CantCast".Translate(skillName, reasonCantCast), () => {})
                {
                    Disabled = true
                };
                continue;
            }

            bool enemy = target.HostileTo(Faction.OfPlayer);
            var priority = enemy ? MenuOptionPriority.AttackEnemy : MenuOptionPriority.VeryLow;

            yield return new FloatMenuOption("AM.Skill.Cast".Translate(skillName, target), () =>
            {
                if (Core.Settings.WarnOfFriendlyExecution)
                {
                    enemy = target.HostileTo(Faction.OfPlayer) || (target.def.race?.Animal ?? false);
                    if (!enemy && !Input.GetKey(KeyCode.LeftShift))
                    {
                        Messages.Message("Tried to cast skill on friendly: hold the Shift key when selecting to confirm!", MessageTypeDefOf.RejectInput, false);
                        return;
                    }
                }

                string msg;
                if (!skill.IsEnabledForPawn(out _) || skill.CanTriggerOn(target) != null)
                {
                    msg = "AM.Skill.CantCastGeneric".Translate(skillName, target);
                    Messages.Message(msg, MessageTypeDefOf.RejectInput, false);
                    return;
                }

                Core.Log($"Attempting cast of {skillName} on {target} by {attacker}");
                if (skill.TryTrigger(target))
                    return;

                msg = "AM.Skill.CantCastGeneric".Translate(skillName, target);
                Messages.Message(msg, MessageTypeDefOf.RejectInput, false);
                
            }, priority, revalidateClickTarget: target);
        }
    }

    private static void HoverAction(Pawn pawn, string tt)
    {
        if (pawn is not { Spawned: true })
            return;

        bool HasLos(IntVec3 c)
        {
            // TODO this should technically be LOS from any free spot around grappler.
            // More expensive to calculate though.
            return GenSight.LineOfSight(pawn.Position, c, pawn.Map, false, c2 => ActionController.LOSValidator(c2, pawn.Map));
        }

        if (Event.current.type == EventType.Repaint && pawn.TryGetLasso() != null)
        {
            pawn.GetAnimManager().AddPostDraw(() =>
            {
                float radius = pawn.GetStatValue(AM_DefOf.AM_GrappleRadius);
                GenDraw.DrawRadiusRing(pawn.Position, radius, Color.yellow, HasLos);
            });
        }

        if (tt == null)
            return;

        var mp = Event.current.mousePosition;
        TooltipHandler.TipRegion(new Rect(mp.x - 1, mp.y - 1, 3, 3), tt);
    }

    private static FloatMenuOption GetDisabledExecutionOption(in ExecutionAttemptReport report, Pawn grappler)
    {
        string label = "AM.Error.Exec.FloatMenu".Translate(report.ErrorMessageShort);
        string tooltip = report.ErrorMessage;
        var icon = Content.ExtraGuiWhy;
        var iconSize = new Rect(0, 0, icon.width, icon.height);

        return new FloatMenuOption(label, null, MenuOptionPriority.DisabledOption)
        {
            tooltip = tooltip,
            Disabled = true,
            extraPartWidth = 54,
            extraPartRightJustified = true,
            extraPartOnGUI = r =>
            {
                var bounds = iconSize.CenteredOnYIn(r).CenteredOnXIn(r);
                var w = bounds.width;
                var c = GUI.color;
                var md = grappler.GetMeleeData();
                float p = md.GetExecuteCooldownPct();


                GUI.color = p < 1f ? Color.red : Color.white;
                Widgets.DrawTexturePart(bounds, new Rect(0, 0, 1, 1), icon);

                if (p < 1f)
                {
                    GUI.color = Color.green * 0.9f;
                    Widgets.DrawTexturePart(bounds with { width = w * p }, new Rect(0, 0, p, 1), icon);
                }

                GUI.color = c;

                if (Mouse.IsOver(r))
                    HoverAction(grappler, tooltip);
                return false;
            }
        };
    }

    private static void ExecutionEnabledOnClick(Pawn target, Pawn attacker, ExecutionAttemptReport report, ExecutionAttemptRequest request)
    {
        if (Core.Settings.WarnOfFriendlyExecution)
        {
            bool enemy = target.HostileTo(Faction.OfPlayer) || (target.def.race?.Animal ?? false);
            if (!enemy && !Input.GetKey(KeyCode.LeftShift))
            {
                Messages.Message("Tried to execute friendly: hold the Shift key when selecting to confirm!", MessageTypeDefOf.RejectInput, false);
                return;
            }
        }

        // Update request.        
        request.OccupiedMask = SpaceChecker.MakeOccupiedMask(attacker.Map, attacker.Position, out _);

        // Get new report
        report.Dispose();
        report = controller.GetExecutionReport(request).FirstOrDefault();
        if (!report.CanExecute)
        {
            Messages.Message(report.ErrorMessage, MessageTypeDefOf.RejectInput, false);
            report.Dispose();
            return;
        }

        // Sanity check:
        if (report.Target != target)
            throw new Exception("Target is not expected target!");

        if (report.IsWalking)
        {
            // Walk to and execute target.
            // Make walk job and reset all verbs (stop firing, attacking).
            var walkJob = JobMaker.MakeJob(AM_DefOf.AM_WalkToExecution, target);

            if (attacker.verbTracker?.AllVerbs != null)
                foreach (var verb in attacker.verbTracker.AllVerbs)
                    verb.Reset();

            if (attacker.equipment?.AllEquipmentVerbs != null)
                foreach (var verb in attacker.equipment.AllEquipmentVerbs)
                    verb.Reset();

            // Start walking.
            JobDriver_GoToExecutionSpot.UseTheseAnimations = request.OnlyTheseAnimations;
            attacker.jobs.StartJob(walkJob, JobCondition.InterruptForced);
            JobDriver_GoToExecutionSpot.UseTheseAnimations = null;

            if (attacker.CurJobDef == AM_DefOf.AM_WalkToExecution)
            {
                return;
            }

            Core.Error($"CRITICAL ERROR: Failed to force interrupt {attacker}'s job with execution goto job. Likely a mod conflict or invalid start parameters.");
            Core.Error($"There were {report.PossibleExecutions?.Count ?? -1} possible executions.");
            Messages.Message("Failed to start execution, possibly no path or bug.", MessageTypeDefOf.RejectInput, false);
        }
        else
        {
            // Check for adjacent:
            var selectedAdjacent = report.PossibleExecutions.Where(p => p.LassoToHere == null).RandomElementByWeightWithFallback(p => (request.OnlyTheseAnimations != null ? 0.1f : 0f) + p.Animation.Probability);
            if (selectedAdjacent.IsValid)
            {
                var outcome = OutcomeUtility.GenerateRandomOutcome(attacker, report.Target, true);
                var anim = outcome == ExecutionOutcome.Failure ? AM_DefOf.AM_Execution_Fail : selectedAdjacent.Animation.AnimDef;

                if (outcome is not (ExecutionOutcome.Failure or ExecutionOutcome.Nothing))
                {
                    // Do animation promotion:
                    anim = anim.TryGetPromotionDef(new AnimDef.PromotionInput
                    {
                        Attacker = attacker,
                        Victim = report.Target,
                        Outcome = outcome,
                        FlipX = selectedAdjacent.Animation.FlipX,
                        OriginalAnim = anim,
                        OccupiedMask = request.OccupiedMask,
                        ReqInput = new ReqInput(attacker.GetFirstMeleeWeapon()?.def)
                    }) ?? anim;
                }

                var startArgs = new AnimationStartParameters(anim, attacker, report.Target)
                {
                    FlipX = selectedAdjacent.Animation.FlipX,
                    ExecutionOutcome = outcome
                };

                if (!startArgs.TryTrigger())
                {
                    Core.Error("Instant adjacent execution (from float menu) failed to trigger!");
                    return;
                }

                // Set cooldown.
                attacker.GetMeleeData().TimeSinceExecuted = 0;
                return;
            }

            // Lasso executions:
            var selectedLasso = report.PossibleExecutions.Where(p => p.LassoToHere != null).RandomElementByWeightWithFallback(p => (request.OnlyTheseAnimations != null ? 0.1f : 0f) + p.Animation.Probability);
            if (selectedLasso.IsValid)
            {
                var outcome = OutcomeUtility.GenerateRandomOutcome(attacker, report.Target, true);
                var anim = outcome == ExecutionOutcome.Failure ? AM_DefOf.AM_Execution_Fail : selectedLasso.Animation.AnimDef;

                if (outcome is not (ExecutionOutcome.Failure or ExecutionOutcome.Nothing))
                {
                    // Do animation promotion:
                    anim = anim.TryGetPromotionDef(new AnimDef.PromotionInput
                    {
                        Attacker = attacker,
                        Victim = report.Target,
                        Outcome = outcome,
                        FlipX = selectedAdjacent.Animation.FlipX,
                        OriginalAnim = anim,
                        OccupiedMask = request.OccupiedMask,
                        ReqInput = new ReqInput(attacker.GetFirstMeleeWeapon()?.def)
                    }) ?? anim;
                }

                var startArgs2 = new AnimationStartParameters(anim, attacker, report.Target)
                {
                    FlipX = selectedLasso.Animation.FlipX,
                    ExecutionOutcome = outcome
                };

                if (!JobDriver_GrapplePawn.GiveJob(attacker, target, selectedLasso.LassoToHere.Value, false, startArgs2))
                {
                    Core.Error($"Failed to give grapple job to {attacker}.");
                    return;
                }

                // Set grapple cooldown.
                attacker.GetMeleeData().TimeSinceGrappled = 0;
                return;
            }

            Core.Warn("Failed to start any execution via adjacent or grapple, maybe they are disabled in the settings.");
        }
    }

    private static FloatMenuOption GetEnabledExecuteOption(ExecutionAttemptRequest request, ExecutionAttemptReport report, Pawn grappler, Pawn target)
    {
        string targetName = target.Name?.ToStringShort ?? target.LabelDefinite();
        bool enemy = target.HostileTo(Faction.OfPlayer) || (target.def.race?.Animal ?? false);
        bool isWalk = report.IsWalking;
        bool isLasso = report.PossibleExecutions?.FirstOrDefault().LassoToHere != null;
        string append = isLasso ? "AM.Exec.FloatMenu.Lasso".Trs() : isWalk ? "AM.Exec.FloatMenu.Walk".Trs() : null;

        string label = "AM.Exec.FloatMenu".Translate(targetName) + append;
        string tt = "AM.Exec.FloatMenu.Tip";
        var probs = new OutcomeUtility.ProbabilityReport();
        OutcomeUtility.GenerateRandomOutcome(grappler, target, true, probs);
        tt = tt.Translate(grappler.Name.ToStringShort, targetName, probs.ToString());
        var priority = enemy ? MenuOptionPriority.AttackEnemy : MenuOptionPriority.VeryLow;

        return new FloatMenuOption(label, () => ExecutionEnabledOnClick(target, grappler, report, request), priority, revalidateClickTarget: target)
        {
            mouseoverGuiAction = _ => HoverAction(grappler, tt)
        };
    }

    private static FloatMenuOption GetDisabledLassoOption(in GrappleAttemptReport report, Pawn grappler)
    {
        string label = "AM.Error.Grapple.FloatMenu".Translate(report.ErrorMessageShort);
        string tooltip = report.ErrorMessage;
        var icon = Content.ExtraGuiWhy;
        var iconSize = new Rect(0, 0, icon.width, icon.height);

        return new FloatMenuOption(label, null, MenuOptionPriority.DisabledOption)
        {
            tooltip = tooltip,
            Disabled = true,
            extraPartWidth = 54,
            extraPartRightJustified = true,
            extraPartOnGUI = r =>
            {
                var bounds = iconSize.CenteredOnYIn(r).CenteredOnXIn(r);
                var c = GUI.color;
                GUI.color = new Color(1, 1, 1, 0.7f);
                Widgets.DrawTexturePart(bounds, new Rect(0, 0, 1, 1), icon);
                GUI.color = c;
                if (Mouse.IsOver(r))
                    HoverAction(grappler, tooltip);
                return false;
            }
        };
    }

    private static FloatMenuOption GetEnabledLassoOption(GrappleAttemptRequest request, GrappleAttemptReport report, Pawn grappler, Pawn target)
    {
        void OnClick()
        {
            // Update request.
            SpaceChecker.MakeOccupiedMask(grappler.Map, grappler.Position, out uint newMask);
            request.OccupiedMask = newMask;
            request.DoNotCheckLasso = false;

            // Get new report
            report = controller.GetGrappleReport(request);
            if (!report.CanGrapple)
            {
                Messages.Message(report.ErrorMessage, MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (!JobDriver_GrapplePawn.GiveJob(grappler, target, report.DestinationCell, true, default))
            {
                Core.Error($"Failed to give grapple job to {grappler}.");
                return;
            }

            // Set grapple cooldown.
            grappler.GetMeleeData().TimeSinceGrappled = 0;
        }

        string targetName = target.Name?.ToStringShort ?? target.LabelDefinite();
        bool enemy = target.HostileTo(Faction.OfPlayer);

        string label = "AM.Grapple.FloatMenu".Translate(targetName);
        string tt = enemy ? "AM.Grapple.FloatMenu.TipEnemy" : "AM.Grapple.FloatMenu.Tip";
        tt = tt.Translate(grappler.Name.ToStringShort, targetName);

        return new FloatMenuOption(label, OnClick, MenuOptionPriority.AttackEnemy, revalidateClickTarget: target)
        {
            mouseoverGuiAction = _ => HoverAction(grappler, tt)
        };
    }
}
