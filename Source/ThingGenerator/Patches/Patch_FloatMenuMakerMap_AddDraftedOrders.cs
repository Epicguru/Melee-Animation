using System.Collections.Generic;
using System.Linq;
using AM.Grappling;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AM.Patches;

[HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.AddDraftedOrders))]
public class Patch_FloatMenuMakerMap_AddDraftedOrders
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
#if !V13
        canTargetCorpses = false,
        canTargetBloodfeeders = true,
#endif
    };

    static void Postfix(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts)
    {
        if (!pawn.IsColonistPlayerControlled)
            return;

        // Downed pawns can't do shit.
        if (pawn.Downed)
            return;

        var weapon = pawn.GetFirstMeleeWeapon();
        var lasso = pawn.TryGetLasso();
        if (weapon == null && lasso == null)
            return;

        ulong occupiedMask = SpaceChecker.MakeOccupiedMask(pawn.Map, pawn.Position, out uint smallMask);

        var targets = GenUI.TargetsAt(clickPos, targetingArgs, true);
        bool noExecEver = false;

        foreach (var t in targets)
        {
            var target = t.Pawn ?? t.Thing as Pawn;
            if (target == null || target.Dead)
                continue;

            // Cannot target self.
            if (target == pawn)
                continue;

            bool isEnemy = target.HostileTo(Faction.OfPlayer);

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

                opts.Add(grappleReport.CanGrapple ? GetEnabledLassoOption(request, grappleReport, pawn, target) : GetDisabledLassoOption(grappleReport, pawn));
            }

            if ((target.def.race?.Animal ?? false) && !Core.Settings.AnimalsCanBeExecuted)
                continue;

            if (weapon != null && !noExecEver)
            {
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
                    if (report is {IsFinal: true, CanExecute: false})
                    {
                        noExecEver = true;
                        opts.Add(GetDisabledExecutionOption(report, pawn));
                        break;
                    }

                    opts.Add(report.CanExecute ? GetEnabledExecuteOption(request, report, pawn, target) : GetDisabledExecutionOption(report, pawn));
                }
            }
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
            return GenSight.LineOfSight(pawn.Position, c, pawn.Map);
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

    private static FloatMenuOption GetEnabledExecuteOption(ExecutionAttemptRequest request, ExecutionAttemptReport report, Pawn grappler, Pawn target)
    {
        void OnClick()
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
            SpaceChecker.MakeOccupiedMask(grappler.Map, grappler.Position, out uint newMask);
            request.OccupiedMask = newMask;

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
                throw new System.Exception("Target is not expected target!");

            if (report.IsWalking)
            {
                // Walk to and execute target.
                // Make walk job and reset all verbs (stop firing, attacking).
                var walkJob = JobMaker.MakeJob(AM_DefOf.AM_WalkToExecution, target);

                if (grappler.verbTracker?.AllVerbs != null)
                    foreach (var verb in grappler.verbTracker.AllVerbs)
                        verb.Reset();

                if (grappler.equipment?.AllEquipmentVerbs != null)
                    foreach (var verb in grappler.equipment.AllEquipmentVerbs)
                        verb.Reset();

                // Start walking.
                grappler.jobs.StartJob(walkJob, JobCondition.InterruptForced);

                if (grappler.CurJobDef == AM_DefOf.AM_WalkToExecution)
                    return;

                Core.Error($"CRITICAL ERROR: Failed to force interrupt {grappler}'s job with execution goto job. Likely a mod conflict or invalid start parameters.");
                string reason = "AM.Gizmo.Error.NoLasso".Translate(
                    new NamedArgument(grappler.NameShortColored, "Pawn"),
                    new NamedArgument(target.NameShortColored, "Target"));
                string error = "AM.Gizmo.Execute.Fail".Translate(new NamedArgument(reason, "Reason"));
                Messages.Message(error, MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                // Check for adjacent:
                var selectedAdjacent = report.PossibleExecutions.Where(p => p.LassoToHere == null).RandomElementByWeightWithFallback(p => p.Animation.Probability);
                if (selectedAdjacent.IsValid)
                {
                    var startArgs = new AnimationStartParameters(selectedAdjacent.Animation.AnimDef, grappler, report.Target)
                    {
                        FlipX = selectedAdjacent.Animation.FlipX,
                        ExecutionOutcome = OutcomeUtility.GenerateRandomOutcome(grappler, report.Target)
                    };

                    if (!startArgs.TryTrigger())
                    {
                        Core.Error("Instant adjacent execution (from float menu) failed to trigger!");
                        return;
                    }

                    // Set cooldown.
                    grappler.GetMeleeData().TimeSinceExecuted = 0;
                    return;
                }

                // Lasso executions:
                var selectedLasso = report.PossibleExecutions.Where(p => p.LassoToHere != null).RandomElementByWeightWithFallback(p => p.Animation.Probability);
                if (selectedLasso.IsValid)
                {
                    var startArgs2 = new AnimationStartParameters(selectedLasso.Animation.AnimDef, grappler, report.Target)
                    {
                        FlipX = selectedLasso.Animation.FlipX,
                        ExecutionOutcome = OutcomeUtility.GenerateRandomOutcome(grappler, report.Target)
                    };

                    if (!JobDriver_GrapplePawn.GiveJob(grappler, target, selectedLasso.LassoToHere.Value, false, startArgs2))
                    {
                        Core.Error($"Failed to give grapple job to {grappler}.");
                        return;
                    }

                    // Set grapple cooldown.
                    grappler.GetMeleeData().TimeSinceGrappled = 0;
                    return;
                }

                Core.Warn("Failed to start any execution via adjacent or grapple, maybe they are disabled in the settings.");
            }
        }

        string targetName = target.Name?.ToStringShort ?? target.LabelDefinite();
        bool enemy = target.HostileTo(Faction.OfPlayer) || (target.def.race?.Animal ?? false);
        bool isWalk = report.IsWalking;
        bool isLasso = report.PossibleExecutions?.FirstOrDefault().LassoToHere != null;
        string append = isLasso ? " (lasso)" : isWalk ? " (walk to target)" : null;

        string label = "AM.Exec.FloatMenu".Translate(targetName) + append;
        string tt = enemy ? "AM.Exec.FloatMenu.TipEnemy" : "AM.Exec.FloatMenu.Tip";
        tt = tt.Translate(grappler.Name.ToStringShort, targetName);
        var priority = enemy ? MenuOptionPriority.AttackEnemy : MenuOptionPriority.VeryLow;

        return new FloatMenuOption(label, OnClick, priority, revalidateClickTarget: target)
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