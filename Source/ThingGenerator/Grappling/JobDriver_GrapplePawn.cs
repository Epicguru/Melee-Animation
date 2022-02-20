using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace AAM.Grappling
{
    public class JobDriver_GrapplePawn : JobDriver
    {
        public static bool GiveJob(Pawn grappler, Pawn target, IntVec3 targetCell, bool ignoreDeadOrDowned = false, AnimationStartParameters? animationStartParameters = null)
        {
            if (grappler == null || !grappler.Spawned || grappler.Dead || grappler.Downed)
                return false;

            if (grappler.CurJobDef == AAM_DefOf.AAM_GrapplePawn)
                return false;

            if (target == null || !target.Spawned || !ignoreDeadOrDowned && (target.Dead || target.Downed))
                return false;

            target.stances.stunner.StunFor(120, grappler, false, false);
            CleanPawn(grappler);

            var newJob = JobMaker.MakeJob(AAM_DefOf.AAM_GrapplePawn, targetCell, target);

            void CleanPawn(Pawn pawn)
            {
                if (pawn.verbTracker?.AllVerbs != null)
                    foreach (var verb in pawn.verbTracker.AllVerbs)
                        verb.Reset();


                if (pawn.equipment?.AllEquipmentVerbs != null)
                    foreach (var verb in pawn.equipment.AllEquipmentVerbs)
                        verb.Reset();
            }

            grappler.jobs.StartJob(newJob, JobCondition.InterruptForced, null, false, true, null);

            if (grappler.jobs.curDriver is not JobDriver_GrapplePawn created)
            {
                Core.Error($"Failed to give job (driver) to pawn {grappler.NameShortColored} even when using InterruptForced: is there another mod interrupting? What is going on...");
            }
            else
            {
                created.AnimationStartParameters = animationStartParameters;
            }

            return true;
        }

        public IntVec3 TargetDestination => this.job.targetA.Cell;
        public Pawn GrappledPawn => this.job?.targetB.Pawn;
        public GrappleFlyer Flyer => GrappledPawn == null ? null : GrappledPawn.ParentHolder as GrappleFlyer;
        public float PullDistance => GrappledPawn.Position.DistanceTo(TargetDestination);

        public AnimationStartParameters? AnimationStartParameters;
        public int TicksToPull;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            job.locomotionUrgency = LocomotionUrgency.None;
            job.collideWithPawns = true;
            job.playerForced = true;

            var lookAtTarget = new Toil();
            lookAtTarget.handlingFacing = true;
            lookAtTarget.initAction = () =>
            {
                pawn.pather.StopDead();
                pawn.rotationTracker.FaceTarget(job.targetB);

                var flyer = GrappleFlyer.MakeGrappleFlyer(pawn, GrappledPawn, TargetDestination);
                if (flyer == null) // creation failed.
                {
                    EndJobWith(JobCondition.Errored);
                    Core.Error($"Failed: failed to spawn flyer.");
                    return;
                }
                TicksToPull = flyer.TotalDurationTicks;
            };
            lookAtTarget.tickAction = () =>
            {
                // Where did our pawn go?
                if (GrappledPawn == null)
                {
                    EndJobWith(JobCondition.Errored);
                    return;
                }

                // Check completion.
                if (Flyer == null)
                {
                    // Upon landing, the flyer de-spawns and the pawn is released.
                    EndJobWith(JobCondition.Succeeded);

                    if (AnimationStartParameters != null)
                    {
                        bool worked = AnimationStartParameters.Value.TryTrigger();
                        if (!worked)
                            Core.Error($"AnimationOnFinish failed to trigger - Invalid object? Invalid state? Invalid pawn(s)? Pawns: {string.Join(", ", AnimationStartParameters.Value.EnumeratePawns())}");
                    }
                    return;
                }

                // If the flyer is still active but does not have a pawn in it, it's not good. Always indicates an error.
                if (Flyer.Spawned && Flyer.FlyingPawn == null)
                {
                    EndJobWith(JobCondition.Errored);
                    return;
                }
            };
            lookAtTarget.defaultCompleteMode = ToilCompleteMode.Never;

            yield return lookAtTarget;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref AnimationStartParameters, "animOnFinish");
        }

        public override string GetReport()
        {
            return $"Grapple: Pulling {GrappledPawn?.NameShortColored} in ({PullDistance}).";
        }
    }
}
