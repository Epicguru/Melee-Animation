using AM.Grappling;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace AM.Jobs;

/*
 * Target A: Duel opponent pawn.
 * Target B: Target standing spot in duel.
 * Target C: Standing spot object itself.
 */
public class JobDriver_DoFriendlyDuel : JobDriver
{
    // Wait for up to a minute for the opponent to arrive.
    public const int MAX_WAIT_TICKS = 60 * 60;

    private bool ShouldFaceEast => TargetThingC.Position == TargetB.Cell;

    private int ticksSpentWaiting;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        // Attempt to reserve destination cell.
        return pawn.Reserve(job.GetTarget(TargetIndex.B), job, errorOnFailed: errorOnFailed);
    }

    public override IEnumerable<Toil> MakeNewToils()
    {
        // Several important checks including melee weapons, spawn status etc.
        AddFailCondition(ShouldJobFail);

        // Opponent mental state check.
        this.FailOnAggroMentalState(TargetIndex.A);
        // Duel spot cannot be forbidden.
        this.FailOnDespawnedNullOrForbidden(TargetIndex.B);
        // Fail if pathing cannot reach target.
        this.FailOnCannotTouch(TargetIndex.B, PathEndMode.OnCell);


        // Walk to destination:
        yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);

        // Wait for opponent to arrive.
        yield return WaitForOpponent();
    }

    private Toil WaitForOpponent()
    {
        var toil = ToilMaker.MakeToil();

        // Handles facing, look towards the enemy side.
        toil.handlingFacing = true;

        toil.tickAction = () =>
        {
            // Make the pawn face the opponent's duel spot while they wait.
            toil.actor.Rotation = ShouldFaceEast ? Rot4.East : Rot4.West;

            // Stop waiting after a while.
            ticksSpentWaiting++;
            if (ticksSpentWaiting > MAX_WAIT_TICKS)
            {
                toil.actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                string txt = "AM.Error.Duel.WaitedTooLong".Translate(toil.actor, job.targetA.Pawn);
                Messages.Message(txt, MessageTypeDefOf.NegativeEvent);
                return;
            }
        };

        // Just wait until I say so!
        toil.defaultCompleteMode = ToilCompleteMode.Never;

        return toil;
    }

    private bool ShouldJobFail()
    {
        bool IsPawnValid(Pawn p)
        {
            if (p == null || p.Dead || p.Downed || !p.Spawned)
                return false;

            if (p.TryGetAnimator() != null)
                return false;

            if (GrabUtility.IsBeingTargetedForGrapple(p))
                return false;

            if (p.GetFirstMeleeWeapon() == null)
                return false;

            if (p != pawn)
            {
                if (p.jobs?.curDriver is not JobDriver_DoFriendlyDuel otherDriver)
                    return false;

                if (otherDriver.TargetThingA as Pawn != pawn)
                    return false;
            }

            return true;
        }

        if (!IsPawnValid(pawn))
            return true;
        if (!IsPawnValid(TargetThingA as Pawn))
            return true;

        return false;
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref ticksSpentWaiting, "ticksSpentWaiting");
    }
}
