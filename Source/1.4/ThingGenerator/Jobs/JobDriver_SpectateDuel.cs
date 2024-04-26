using AM.Buildings;
using AM.Idle;
using JetBrains.Annotations;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AM.Jobs;

[UsedImplicitly]
/*
 * Target A: Duel Spot Thing
 * Target B: Spectate cell
 * Target C: One of the two pawns that are dueling, normally the one that this pawn is rooting for.
 */
public class JobDriver_SpectateDuel : JobDriver
{
    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(job.GetTarget(TargetIndex.B), job, errorOnFailed: errorOnFailed);
    }

    public override IEnumerable<Toil> MakeNewToils()
    {
        job.locomotionUrgency = LocomotionUrgency.Sprint;

        // Duel spot cannot be forbidden.
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        // Duel must still be running obv.
        AddFailCondition(DuelIsNotRunning);

        // Reserve duel spot:
        yield return Toils_Reserve.ReserveDestination(TargetIndex.B);

        // Walk to duel spot:
        yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);

        // Look at duel.
        yield return WaitForEndOfDuel();
    }

    private Toil WaitForEndOfDuel()
    {
        var toil = ToilMaker.MakeToil();

        toil.defaultCompleteMode = ToilCompleteMode.Never;
        toil.socialMode = RandomSocialMode.SuperActive;
        toil.activeSkill = () => SkillDefOf.Social;
        toil.handlingFacing = true;

        var idleAnim = pawn.GetComp<IdleControllerComp>();

        toil.tickAction = () =>
        {
            // Look at duel.
            pawn.rotationTracker.Face(TargetA.Cell.ToVector3Shifted() + new Vector3(0.5f, 0, 0));

            // Gain joy and recreation.
            if (pawn.needs.joy != null)
                JoyUtility.JoyTickCheckEnd(pawn, JoyTickFullJoyAction.None);

            if (idleAnim == null)
                return;

            // TODO maybe do cheering animation.
            //idleAnim.
        };

        return toil;
    }

    private bool DuelIsNotRunning()
    {
        var ds = TargetA.Thing as Building_DuelSpot;

        if (ds.DestroyedOrNull())
            return true;

        // Does not really belong here...
        if (pawn.InMentalState)
            return true;

        return !ds.IsInUse(out _, out _);
    }
}