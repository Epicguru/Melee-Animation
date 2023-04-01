using AM.AutoDuel;
using AM.Controller;
using JetBrains.Annotations;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Noise;

namespace AM.Buildings;

[UsedImplicitly]
public class Building_DuelSpot : Building
{
    private static readonly HashSet<Pawn> tempReservers = new HashSet<Pawn>();

    public IntVec3 CellMain => Position;
    public IntVec3 CellOther => Position + new IntVec3(1, 0, 0);
    public bool IsForbidden
    {
        get
        {
            forbidComp ??= GetComp<CompForbiddable>();
            return forbidComp?.Forbidden ?? false;
        }
    }

    private CompForbiddable forbidComp;

    public bool IsInUse()
    {
        bool IsReserved(in IntVec3 cell)
        {
            tempReservers.Clear();
            Map.reservationManager.ReserversOf(cell, tempReservers);
            bool any = tempReservers.Count > 0;
            tempReservers.Clear();
            return any;
        }

        return IsReserved(CellMain) || IsReserved(CellOther);
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        foreach (var t in base.GetGizmos())
            yield return t;

        var cmd = new Command_Action
        {
            action = ClickedStartDuel,
            defaultLabel = "AM.Gizmos.DuelSpot.StartDuel.Label".Trs(),
            defaultDesc = "AM.Gizmos.DuelSpot.StartDuel.Desc".Trs(),
            icon = Content.DuelIcon,
            defaultIconColor = Color.green
        };

        if (IsInUse())
        {
            cmd.disabled = true;
            cmd.disabledReason = "AM.Gizmos.DuelSpot.StartDuel.InUse".Trs();
        }
        else if (IsForbidden)
        {
            cmd.disabled = true;
            cmd.disabledReason = "AM.Gizmos.DuelSpot.StartDuel.Forbidden".Trs();
        }

        yield return cmd;
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

        void MakeDisabled(Pawn p, string reasonTrs, NamedArgument arg = default)
        {
            items.Add(new FloatMenuOption(p.Name?.ToStringShort ?? p.LabelCap, () => { }, MenuOptionPriority.Low)
            {
                iconThing = p,
                tooltip = reasonTrs.Translate(p, except, arg),
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

            // Job must be interrupted:
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

            var md = p.GetMeleeData();

            // Duel is not on cooldown.
            if (!md.IsFriendlyDuelOffCooldown())
            {
                MakeDisabled(p, "AM.Gizmos.DuelSpot.StartDuel.OnCooldown", md.GetFriendlyDuelRemainingCooldownSeconds().ToString("F1"));
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
        var job = MakeDuelJob(b, true);
        job.playerForced = true;

        a.jobs.StartJob(job, JobCondition.InterruptForced);
        if (a.CurJobDef != AM_DefOf.AM_DoFriendlyDuel)
        {
            Messages.Message("AM.Gizmos.DuelSpot.StartDuel.FailedToStart".Trs(), MessageTypeDefOf.RejectInput, false);
            Core.Error($"Failed to give {a} the friendly duel job, probably mod incompatibility.");
            return;
        }

        job = MakeDuelJob(a, false);
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

    public Job MakeDuelJob(Pawn opponent, bool main)
    {
        return JobMaker.MakeJob(AM_DefOf.AM_DoFriendlyDuel, new LocalTargetInfo(opponent), new LocalTargetInfo(main ? CellMain : CellOther), new LocalTargetInfo(this));
    }

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);

        var comp = map.GetComponent<AutoFriendlyDuelMapComp>();
        if (comp == null)
        {
            Core.Error($"Missing {nameof(AutoFriendlyDuelMapComp)} map component.");
            return;
        }

        if (!comp.DuelSpots.Add(this))
            Core.Error("Attempted to duplicate-add duel spot to map component - why and how?");
    }

    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        var comp = Map.GetComponent<AutoFriendlyDuelMapComp>();
        if (comp == null)
        {
            Core.Error($"Missing {nameof(AutoFriendlyDuelMapComp)} map component.");
            return;
        }

        if (!comp.DuelSpots.Remove(this))
            Core.Error("Attempted to duplicate-remove duel spot to map component - why and how?");

        base.Destroy(mode);
    }
}
