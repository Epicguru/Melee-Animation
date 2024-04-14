using JetBrains.Annotations;
using RimWorld;
using Verse;
using Verse.AI;

namespace AM.AutoDuel;

[UsedImplicitly]
public class JoyGiver_FriendlyDuel : JoyGiver
{
    public override Job TryGiveJob(Pawn pawn)
    {
        // Duel on cooldown check:
        if (!pawn.GetMeleeData().IsFriendlyDuelOffCooldown())
            return null;

        // Get map comp to see if this pawn is eligible:
        var comp = pawn.Map?.GetComponent<AutoFriendlyDuelMapComp>();
        if (comp == null)
            return null;

        // The comp will cache any pawns that have valid melee weapons:
        if (!comp.CanPawnMaybeDuel(pawn))
            return null;

        // Check still valid (cache could be out of date):
        if (!AutoFriendlyDuelMapComp.CanPawnDuel(pawn))
            return null;

        // Try to get a duel partner.
        var partner = comp.TryGetRandomDuelPartner(pawn);
        if (partner == null)
            return null;

        // Try to get a duel spot for us two:
        var spot = comp.TryGetBestDuelSpotFor(pawn, partner);
        if (spot == null)
            return null;

        // Try give job to opponent:
        Core.Log($"Interrupting {partner}'s {partner.CurJob} to start a friendly duel with {pawn}, auto mode.");

        var opJob = spot.MakeDuelJob(pawn, false);
        partner.jobs.StartJob(opJob, JobCondition.InterruptForced);
        if (partner.CurJobDef != AM_DefOf.AM_DoFriendlyDuel)
        {
            Core.Error($"Failed to give {partner} the friendly duel job (automatic), probably mod incompatibility.");
            return null;
        }

        return spot.MakeDuelJob(partner, true);
    }
}