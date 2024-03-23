using AM.Buildings;
using AM.Tweaks;
using JetBrains.Annotations;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Verse;
#if !V14
using LudeonTK;
#endif

namespace AM.AutoDuel;

[UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
public class AutoFriendlyDuelMapComp : MapComponent
{
    [UsedImplicitly(ImplicitUseKindFlags.Access)]
    [DebugAction("Melee Animation", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void LogAutoDuelInfo()
    {
        var map = Find.CurrentMap;
        if (map == null)
            return;

        var comp = map.GetComponent<AutoFriendlyDuelMapComp>();
        Core.Log($"There are {comp.DuelSpots.Count} duel spots on the map.");
        Core.Log($"Spots with active duels: {comp.GetActiveDuelSpots()}");

        Core.Log("All possible pawns (not filtered):");
        foreach (var p in comp.EnumeratePossiblePawns())
        {
            Core.Log(p.ToString());
        }

        Core.Log("All possible pawns (filtered and cached):");
        foreach (var p in comp.pawnsThatCanDuel)
        {
            Core.Log(p.ToString());
        }
    }

    public readonly HashSet<Building_DuelSpot> DuelSpots = new HashSet<Building_DuelSpot>();

    private readonly HashSet<Pawn> pawnsThatCanDuel = new HashSet<Pawn>();

    public AutoFriendlyDuelMapComp(Map map) : base(map) { }

    public bool CanPawnMaybeDuel(Pawn pawn) => pawnsThatCanDuel.Contains(pawn);

    public Pawn TryGetRandomDuelPartner(Pawn except)
    {
        if (pawnsThatCanDuel.Count <= 1)
            return null;

        return pawnsThatCanDuel.Except(except).Where(CanPawnDuel).RandomElementWithFallback();
    }

    public Building_DuelSpot TryGetBestDuelSpotFor(Pawn a, Pawn b)
    {
        var spots = from s in DuelSpots
                    where !s.IsForbidden && !s.IsForbidden(a) && !s.IsForbidden(b) && !s.IsInUse(out _, out _)
                    let dst = s.Position.DistanceToSquared(a.Position) + s.Position.DistanceToSquared(b.Position)
                    orderby dst
                    select s;

        return spots.FirstOrDefault();
    }

    public IEnumerable<ActiveDuelSpot> GetActiveDuelSpots()
    {
        foreach (var spot in DuelSpots)
        {
            if (spot.IsInUse(out var a, out var b))
            {
                yield return new ActiveDuelSpot
                {
                    Spot = spot,
                    PawnA = a,
                    PawnB = b
                };
            }
        }
    }

    public override void MapComponentTick()
    {
        base.MapComponentTick();

        if (Find.TickManager.TicksAbs % (60 * 1) != 0)
            return;

        try
        {
            UpdatePawnsThatCanDuel();
        }
        catch (Exception e)
        {
            Core.Error("Exception updating friendly duel pawn cached list.", e);
        }
    }

    private void UpdatePawnsThatCanDuel()
    {
        pawnsThatCanDuel.Clear();

        if (Core.Settings.MaxProcessingThreads != 1)
        {
            // Threaded find.
            Parallel.ForEach(EnumeratePossiblePawns(), p =>
            {
                if (!CanPawnDuel(p))
                    return;

                lock (pawnsThatCanDuel)
                {
                    pawnsThatCanDuel.Add(p);
                }
            });
        }
        else
        {
            pawnsThatCanDuel.AddRange(EnumeratePossiblePawns().Where(CanPawnDuel));
        }
    }

    private IEnumerable<Pawn> EnumeratePossiblePawns()
    {
        foreach (var p in map.mapPawns.FreeColonistsSpawned)
            yield return p;
        foreach (var p in map.mapPawns.SlavesOfColonySpawned)
            yield return p;
    }

    public static bool CanPawnDuel(Pawn pawn)
    {
        // Not dead, downed or drafted.
        if (pawn == null || pawn.Dead || pawn.Downed || pawn.Drafted)
            return false;

        // Pawn must have weapon.
        var weapon = pawn.GetFirstMeleeWeapon(out var td);
        if (weapon == null)
            return false;

        const MeleeWeaponType TYPE_MASK = MeleeWeaponType.Long_Stab  |
                                          MeleeWeaponType.Long_Sharp |
                                          MeleeWeaponType.Long_Blunt;

        // And that weapon must be long (for now).
        if ((td.MeleeWeaponType & TYPE_MASK) == 0)
            return false;

        // Duel must not be on cooldown:
        if (!pawn.GetMeleeData().IsFriendlyDuelOffCooldown())
            return false;

        // Check pawn is in recreation and not doing anything majorly important.
        if (pawn.timetable.CurrentAssignment != TimeAssignmentDefOf.Joy || !(pawn.CurJobDef?.playerInterruptible ?? true))
            return false;

        return true;
    }
}

public readonly struct ActiveDuelSpot
{
    public Building_DuelSpot Spot { get; init; }
    public Pawn PawnA { get; init; }
    public Pawn PawnB { get; init; }
}
