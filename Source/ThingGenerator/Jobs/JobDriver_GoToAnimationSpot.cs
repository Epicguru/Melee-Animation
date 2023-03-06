using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace AM.Jobs
{
    public abstract class JobDriver_GoToAnimationSpot : JobDriver
    {
        public Pawn Target => job?.GetTarget(TargetIndex.A).Pawn;
        public Pawn Self => pawn;
        public IntVec3 Offset;

        protected Toil MakeGoToToil()
        {
            job.playerForced = true;

#if V13
            var toil = new Toil();
#else
            var toil = ToilMaker.MakeToil();
#endif

            // Stop if target is despawned (includes flying/grappled)
            this.FailOnDespawnedOrNull(TargetIndex.A);
            // Stop if invalid or destroyed (includes killed, moved to different map).
#if V13
            this.FailOnDestroyedOrNull(TargetIndex.A);
#else
            this.FailOnInvalidOrDestroyed(TargetIndex.A);
#endif
            // Fail if target is down.
            this.FailOnDowned(TargetIndex.A);

            void MoveToTarget(bool forceNewPath)
            {
                if (Target == null)
                    return;

                Offset = Target.Position.x < toil.actor.Position.x ? new IntVec3(1, 0, 0) : new IntVec3(-1, 0, 0);

                // Get current destination.
                IntVec3 destPos = Target.Position + Offset;

                // Check if the destination has been reached.
                if (toil.actor.Position == destPos)
                {
                    toil.actor.pather.StopDead();
                    toil.actor.jobs.curDriver.ReadyForNextToil();
                    return;
                }

                // Check that the destination can still be reached.
                if (!toil.actor.CanReach(destPos, PathEndMode.OnCell, Danger.Deadly))
                {
                    // Pawn cannot reach the required spot.
                    Core.Warn($"{Self} cannot reach target cell {destPos} to reach {Target}.");
                    toil.actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                    return;
                }

                // Update pather destination if required.
                // Update if:
                // - Required by parameter
                // - The pawn has stopped moving for any reason (stunned, pathing error etc)
                // - The target pawn has moved away from the place the pawn is moving towards
                const int RANGE = 2;
                bool targetTooFar = toil.actor.pather.Destination.Cell.DistanceToSquared(destPos) >= (RANGE * RANGE);
                if (forceNewPath || !toil.actor.pather.Moving || targetTooFar)
                {
                    toil.actor.pather.StartPath(destPos, PathEndMode.OnCell);
                }
            }

            // Start action.
            toil.initAction = () =>
            {
                MoveToTarget(true);
            };

            // Tick action.
            toil.tickAction = () =>
            {
                MoveToTarget(false);
            };

            // Fail if melee weapon is lost.
            toil.AddFailCondition(() => toil.actor.GetFirstMeleeWeapon() == null);

            // Fail if target gets trapped by another animation.
            toil.AddFailCondition(() => Target?.TryGetAnimator() != null);

            // No social stuff, doesn't end until I say so.
            toil.socialMode = RandomSocialMode.Off;
            toil.defaultCompleteMode = ToilCompleteMode.Never;

            return toil;
        }

        protected abstract Toil MakeEndToil();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Target, job, 1, -1, null, errorOnFailed);
        }

        public override IEnumerable<Toil> MakeNewToils()
        {
            job.locomotionUrgency = LocomotionUrgency.Sprint;

            yield return MakeGoToToil();
            yield return MakeEndToil();
        }
    }
}
