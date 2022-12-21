using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace AAM.Jobs
{
    public class JobDriver_GoToExecution : JobDriver
    {
        public Pawn Target => job?.GetTarget(TargetIndex.A).Pawn;
        public Pawn Self => pawn;
        public IntVec3 Offset;

        private readonly HashSet<AnimDef> except = new HashSet<AnimDef>();

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
                //bool targetTooFar = toil.actor.pather.Destination.Cell.DistanceToSquared(destPos) > 3;
                if (forceNewPath || !toil.actor.pather.Moving && toil.actor.pather.Destination.Cell != destPos)// || targetTooFar)
                {
                    Core.Log($"Start new path to {destPos}");
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

        protected virtual Toil MakeEndToil()
        {
#if V13
            var toil = new Toil();
#else
            var toil = ToilMaker.MakeToil();
#endif
            // Try do an execution animation.
            toil.initAction = () =>
            {
                if (Target == null)
                    return;

                // Position and flip status.
                IntVec3 targetPos = Target.Position;
                bool flipX = targetPos.x < pawn.Position.x;

                // Get weapon, possible animations, and space mask.
                var weaponDef = pawn.GetFirstMeleeWeapon().def;
                var possibilities = AnimDef.GetExecutionAnimationsForPawnAndWeapon(pawn, weaponDef);
                ulong occupiedMask = SpaceChecker.MakeOccupiedMask(pawn.Map, pawn.Position, out _);

                if (!possibilities.Any())
                {
                    Core.Warn("No possible execution animations after reaching target (none by weapon/pawn)!");
                    toil.actor.jobs.curDriver.EndJobWith(JobCondition.Errored);
                    return;
                }

                // Check space...
                except.Clear();
                while (true)
                {
                    // Pick random anim, weighted.
                    var anim = possibilities.RandomElementByWeightExcept(d => d.Probability, except);
                    if (anim == null)
                    {
                        Core.Warn("No possible execution animations after reaching target (no space)!");
                        return;
                    }

                    except.Add(anim);

                    // Do we have space for this animation?
                    ulong animMask = flipX ? anim.FlipClearMask : anim.ClearMask;
                    ulong result = animMask & occupiedMask; // The result should be 0.

                    if (result == 0)
                    {
                        // Can do the animation!
                        var args = new AnimationStartParameters(anim, toil.actor, Target)
                        {
                            ExecutionOutcome = OutcomeUtility.GenerateRandomOutcome(toil.actor, Target),
                            FlipX = flipX,
                        };

                        // Trigger the execution animation.
                        bool worked = args.TryTrigger();
                        if (worked)
                        {
                            // Reset execution cooldown.
                            toil.actor.GetMeleeData().TimeSinceExecuted = 0;
                        }
                        else
                        {
                            Core.Warn($"Failed to trigger {anim} on {Target}, possibly invalid pawn or in another animation?");
                            toil.actor.jobs.curDriver.EndJobWith(JobCondition.Errored);
                            return;
                        }

                        // Finished.
                        return;
                    }
                }
            };

            toil.defaultCompleteMode = ToilCompleteMode.Instant;

            return toil;
        }

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
