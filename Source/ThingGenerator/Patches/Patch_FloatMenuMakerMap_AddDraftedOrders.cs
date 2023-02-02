using AAM.Grappling;
using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AAM.Patches;

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

        SpaceChecker.MakeOccupiedMask(pawn.Map, pawn.Position, out uint smallMask);

        var targets = GenUI.TargetsAt(clickPos, targetingArgs, true);

        foreach (var t in targets)
        {
            var target = t.Pawn ?? t.Thing as Pawn;
            if (target.Dead)
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
                var report = controller.GetGrappleReport(request);

                opts.Add(report.CanGrapple ? GetEnabledLassoOption(request, report, pawn, target) : GetDisabledLassoOption(report, pawn));
            }
        }
    }

    private static void HoverAction(Pawn pawn, string tt)
    {
        if (pawn is not { Spawned: true })
            return;

        bool HasLos(IntVec3 c)
        {
            return GenSight.LineOfSight(pawn.Position, c, pawn.Map);
        }

        if (Event.current.type == EventType.Repaint)
        {
            pawn.GetAnimManager().AddPostDraw(() =>
            {
                float radius = pawn.GetStatValue(AAM_DefOf.AAM_GrappleRadius);
                GenDraw.DrawRadiusRing(pawn.Position, radius, Color.yellow, HasLos);
            });
        }

        if (tt == null)
            return;

        var mp = Event.current.mousePosition;
        TooltipHandler.TipRegion(new Rect(mp.x - 1, mp.y - 1, 3, 3), tt);
    }

    //private static FloatMenuOption GetDisabledExecutionOption()
    //{
    //    string label = "AAM.Error.Exec.FloatMenu".Translate(report.ErrorMessageShort);

    //}

    private static FloatMenuOption GetDisabledLassoOption(in GrappleAttemptReport report, Pawn grappler)
    {
        string label = "AAM.Error.Grapple.FloatMenu".Translate(report.ErrorMessageShort);
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

        string label = "AAM.Grapple.FloatMenu".Translate(targetName);
        string tt = enemy ? "AAM.Grapple.FloatMenu.TipEnemy" : "AAM.Grapple.FloatMenu.Tip";
        tt = tt.Translate(grappler.Name.ToStringShort, targetName);

        return new FloatMenuOption(label, OnClick, MenuOptionPriority.AttackEnemy,revalidateClickTarget: target)
        {
            mouseoverGuiAction = _ => HoverAction(grappler, tt)
        };
    }
}