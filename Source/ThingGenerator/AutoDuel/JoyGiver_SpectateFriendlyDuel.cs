using System.Linq;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AM.AutoDuel;

[UsedImplicitly]
public class JoyGiver_SpectateFriendlyDuel : JoyGiver
{
    public override Job TryGiveJob(Pawn pawn)
    {
        // Get map comp to see if this pawn is eligible:
        var comp = pawn.Map?.GetComponent<AutoFriendlyDuelMapComp>();
        if (comp == null)
            return null;

        // Get active duels:
        var actives = from spot in comp.GetActiveDuelSpots()
                      orderby spot.Spot.Position.DistanceToSquared(pawn.Position)
                      let spectateCells = spot.Spot.GetFreeSpectateSpots()
                      where spectateCells.Any()
                      select (spot, spectateCells);

        var selected = actives.FirstOrDefault();
        if (selected.spot.Spot == null)
            return null;

        float center = selected.spot.Spot.Position.x + 0.5f;

        // Get random cell, prefer cells closer to the action.
        var cell = selected.spectateCells.RandomElementByWeight(c => 10f - Mathf.Abs(c.x + 0.5f - center));

        // Check that the cell can be reached.
        if (!pawn.CanReach(cell, PathEndMode.OnCell, Danger.Some))
            return null;

        // What pawn to root for? (cosmetic only)
        // Choose duel participant whom this pawn has the higher opinion of.
        var opA = pawn.relations.OpinionOf(selected.spot.PawnA);
        var opB = pawn.relations.OpinionOf(selected.spot.PawnB);
        var rootFor = opA > opB ? selected.spot.PawnA : selected.spot.PawnB;

        // Make job!
        return JobMaker.MakeJob(AM_DefOf.AM_SpectateFriendlyDuel, selected.spot.Spot, cell, rootFor);
    }
}