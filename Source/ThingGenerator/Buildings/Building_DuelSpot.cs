using AM.Controller;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace AM.Buildings;

public class Building_DuelSpot : Building
{
    public IntVec3 CellMain => Position;
    public IntVec3 CellOther => Position + new IntVec3(1, 0, 0);

    public bool IsCurrentlyOccupied(out Thing blocking)
    {
        var m = Map;

        bool Check(in IntVec3 cell, out Thing blocker)
        {
            var things = m.thingGrid.ThingsListAtFast(cell);
            foreach (var thing in things)
            {
                blocker = thing;

                if (thing is Pawn p && (p.IsInAnimation() || p.CurJobDef == AM_DefOf.AM_DoFriendlyDuel))
                    return true;
                if (thing.def.passability == Traversability.Impassable)
                    return true;
            }

            blocker = null;
            return false;
        }

        return Check(CellMain, out blocking) || Check(CellOther, out blocking);
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        foreach (var t in base.GetGizmos())
            yield return t;

        bool occ = IsCurrentlyOccupied(out var blocker);

        yield return new Command_Action
        {
            action = ClickedStartDuel,
            defaultLabel = "AM.Gizmos.DuelSpot.StartDuel.Label".Trs(),
            defaultDesc = "AM.Gizmos.DuelSpot.StartDuel.Desc".Trs(),
            disabled = occ,
            disabledReason = occ ? "AM.Gizmos.DuelSpot.StartDuel.InUse".Trs(blocker) : null,
        };
    }

    private void ClickedStartDuel()
    {
        SelectPawn(null, first =>
        {
            SelectPawn(first, second =>
            {
                Core.Log($"Attempting to start duel spot duel between {first} and {second}");
                TryStartDuel(first, second);
            }, first.GetFirstMeleeWeapon());
        }, null);
    }

    private void SelectPawn(Pawn except, Action<Pawn> onSelected, Thing firstWeapon)
    {
        // Find all pawns that can duel:
        var m = Map;
        var controlledPawns = from pawn in m.mapPawns.AllPawnsSpawned
                              where !pawn.Dead && !pawn.Downed && !pawn.IsInAnimation() && !pawn.HostileTo(Faction.OfPlayer) && !pawn.IsPrisoner && !pawn.def.race.Animal
                              select pawn;

        var items = new List<FloatMenuOption>();

        void MakeDisabled(Pawn p, string reasonTrs)
        {
            items.Add(new FloatMenuOption(p.Name?.ToStringShort ?? p.LabelCap, () => { }, MenuOptionPriority.Low)
            {
                iconThing = p,
                tooltip = reasonTrs.Translate(p, except),
                Disabled = true
            });
        }
        void MakeEnabled(Pawn p)
        {
            items.Add(new FloatMenuOption(p.Name?.ToStringShort ?? p.LabelCap, () => onSelected(p), MenuOptionPriority.Low)
            {
                iconThing = p,
            });
        }

        foreach (var p in controlledPawns)
        {
            if (p == except)
                continue;

            // Must have a weapon:
            var weapon = p.GetFirstMeleeWeapon();
            if (weapon == null)
            {
                MakeDisabled(p, "AM.Gizmos.DuelSpot.StartDuel.MissingWeapon");
                continue;
            }

            // Make sure than an animation can play out between these weapons:
            if (firstWeapon != null)
            {
                var anim = ActionController.TryGetDuelAnimationFor(weapon, firstWeapon, out _);
                if (anim == null)
                {
                    MakeDisabled(p, "AM.Gizmos.DuelSpot.StartDuel.IncompatWeapon");
                    continue;
                }
            }

            // Just must be interrupted:
            if (p.CurJob != null && !p.CurJob.def.playerInterruptible)
            {
                MakeDisabled(p, "AM.Gizmos.DuelSpot.StartDuel.CannotInterrupt");
                continue;
            }

            // Has a path:
            if (!p.CanReserveAndReach(new LocalTargetInfo(CellMain), PathEndMode.OnCell, Danger.Some))
            {
                MakeDisabled(p, "AM.Gizmos.DuelSpot.StartDuel.NoPath");
                continue;
            }

            // Good to go:
            MakeEnabled(p);
        }

        // Open menu.
        Find.WindowStack.Add(new FloatMenu(items, "AM.Gizmos.DuelSpot.StartDuel.FloatMenuTitle".Trs()));
    }

    private void TryStartDuel(Pawn a, Pawn b)
    {
        var job = JobMaker.MakeJob(AM_DefOf.AM_DoFriendlyDuel, new LocalTargetInfo(b), new LocalTargetInfo(CellMain), new LocalTargetInfo(this));
        job.playerForced = true;

        a.jobs.StartJob(job, JobCondition.InterruptForced);
        if (a.CurJobDef != AM_DefOf.AM_DoFriendlyDuel)
        {
            Messages.Message("AM.Gizmos.DuelSpot.StartDuel.FailedToStart".Trs(), MessageTypeDefOf.RejectInput, false);
            Core.Error($"Failed to give {a} the friendly duel job, probably mod incompatibility.");
            return;
        }

        job = JobMaker.MakeJob(AM_DefOf.AM_DoFriendlyDuel, new LocalTargetInfo(a), new LocalTargetInfo(CellOther), new LocalTargetInfo(this));
        job.playerForced = true;

        b.jobs.StartJob(job, JobCondition.InterruptForced);
        if (b.CurJobDef != AM_DefOf.AM_DoFriendlyDuel)
        {
            Messages.Message("AM.Gizmos.DuelSpot.StartDuel.FailedToStart".Trs(), MessageTypeDefOf.RejectInput, false);
            Core.Error($"Failed to give {b} the friendly duel job, probably mod incompatibility.");
            return;
        }

        Core.Log($"Player started duel between {a} and {b}");
    }
}
