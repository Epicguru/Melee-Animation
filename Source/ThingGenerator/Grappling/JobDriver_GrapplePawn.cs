using RimWorld;
using System.Collections.Generic;
using UnityEngine;
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

            if (!GrabUtility.TryRegisterGrabAttempt(target))
                return false;

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
                GrabUtility.EndGrabAttempt(target);
            }
            else
            {
                created.AnimationStartParameters = animationStartParameters;
            }

            return true;
        }

        public static void DrawEnsnaringRope(Pawn grappler, Job job)
        {
            var driver = grappler.jobs.curDriver as JobDriver_GrapplePawn;
            
            if (driver?.GrappledPawn == null || driver.HasEnsnared || driver.RopeDistance == 0)
                return;

            Vector3 start = driver.pawn.DrawPos;
            Vector3 end = driver.GrappledPawn.DrawPos;
            Vector3 realEnd = start + (end - start).normalized * driver.RopeDistance;

            Color ropeColor = grappler?.TryGetLasso()?.def.graphicData.color ?? Color.magenta;

            GrabUtility.DrawRopeFromTo(start, realEnd, ropeColor);
        }

        public IntVec3 TargetDestination => this.job.targetA.Cell;
        public Pawn GrappledPawn => this.job?.targetB.Pawn;
        public GrappleFlyer Flyer => GrappledPawn?.ParentHolder as GrappleFlyer;

        public AnimationStartParameters? AnimationStartParameters;
        public float RopeDistance;
        public bool HasEnsnared;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        private void Ensnare()
        {
            if (HasEnsnared)
            {
                Core.Error("Already called Ensnare");
                return;
            }

            HasEnsnared = true;
            var flyer = GrappleFlyer.MakeGrappleFlyer(pawn, GrappledPawn, TargetDestination);
            if (flyer == null) // creation failed.
            {
                EndJobWith(JobCondition.Errored);
                Core.Error("Failed: failed to spawn flyer.");
            }
            GrabUtility.EndGrabAttempt(GrappledPawn);
        }

        private void TickEnsnared()
        {
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
                    else if (AnimationStartParameters.Value.Animation.type == AnimType.Execution)
                        pawn.GetMeleeData().TimeSinceExecuted = 0;
                }
                return;
            }

            // If the flyer is still active but does not have a pawn in it, it's not good. Always indicates an error.
            if (Flyer.Spawned && Flyer.FlyingPawn == null)
                EndJobWith(JobCondition.Errored);
        }

        private void TickPreEnsnare()
        {
            float lassoFactor = pawn.GetStatValue(AAM_DefOf.AAM_GrappleSpeed);
            RopeDistance += 0.75f * Core.Settings.GrappleSpeed * lassoFactor;

            // TODO maybe don't check every frame?
            var target = GrappledPawn;
            if (!GenSight.LineOfSightToThing(TargetDestination, target, Map) || target.Dead || !target.Spawned || target.IsInAnimation())
            {
                EndJobWith(JobCondition.Errored);
                Core.Warn("Failed: pawn moved out of line of sight before being ensnared.");
                return;
            }

            if(RopeDistance * RopeDistance >= pawn.Position.DistanceToSquared(GrappledPawn.Position))
            {
                Ensnare();
            }
        }

        public override IEnumerable<Toil> MakeNewToils()
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
            };
            lookAtTarget.tickAction = () =>
            {
                // Where did our pawn go?
                if (GrappledPawn == null)
                {
                    EndJobWith(JobCondition.Errored);
                    return;
                }

                if (HasEnsnared)
                    TickEnsnared();
                else
                    TickPreEnsnare();
            };
            
            lookAtTarget.defaultCompleteMode = ToilCompleteMode.Never;

            yield return lookAtTarget;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref AnimationStartParameters, "animOnFinish");
            Scribe_Values.Look(ref HasEnsnared, "hasEnsnared");
            Scribe_Values.Look(ref RopeDistance, "ropeDistance");
        }

        public override string GetReport()
        {
            if(HasEnsnared)
                return $"Using lasso: Pulling {GrappledPawn?.NameShortColored}.";
            return $"Using lasso: Trying to ensnare {GrappledPawn?.NameShortColored}.";
        }
    }
}
