using System.Collections.Generic;
using UnityEngine;
using Verse.AI;

namespace AAM
{
    public class JobDriver_PrepareAnimation : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var goTo = Toils_Goto.GotoCell(TargetIndex.C, PathEndMode.OnCell);
            goTo.AddPreTickAction(KeepTargetStunned);
            goTo.AddFailCondition(IsTargetDeadOrDown);

            yield return goTo;

            yield return Toils_General.Do(() =>
            {
                Core.Log("Reached target position!");
                // Find pawns to do animation on.

                var mainPawn = pawn;
                var otherPawn = TargetB.Pawn;

                // Make transform centered around the main pawn's position.
                var rootTransform = mainPawn.MakeAnimationMatrix();
                Core.Log(rootTransform.MultiplyPoint3x4(Vector2.zero).ToString());

                // Get the current map's animation manager.
                var manager = pawn.Map.GetAnimManager();

                // Cancel any previous animation(s)
                manager.StopAnimation(mainPawn);
                manager.StopAnimation(otherPawn);

                // Try to find an execution animation to play.
                var exec = AnimDef.TryGetExecutionFor(mainPawn, otherPawn);

                if (exec == null)
                {
                    Core.Warn($"Could not find any execution animation to play!");
                    return;
                }

                // Start this new animation.
                bool mirrorX = otherPawn.Position.x < TargetC.Cell.x;
                var anim = manager.StartAnimation(exec, rootTransform, mirrorX, mainPawn, otherPawn);
            });
        }

        public override void ExposeData()
        {            
            base.ExposeData();
        }

        public void KeepTargetStunned()
        {
            var pawnA = TargetA.Pawn;
            var pawnB = TargetB.Pawn;
            pawnB.stances.stunner.StunFor(1, pawnA, false, false);
        }

        public bool IsTargetDeadOrDown()
        {
            // TODO or is in animation
            var victim = TargetB.Pawn;
            return victim == null || victim.Destroyed || !victim.Spawned || victim.Dead || victim.Downed;
        }

        public override string GetReport()
        {
            return $"Preparing for animation...";
        }
    }
}
