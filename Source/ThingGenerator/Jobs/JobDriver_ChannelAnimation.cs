using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace AM.Jobs
{
    public class JobDriver_ChannelAnimation : JobDriver
    {
        public AnimRenderer Animator
        {
            get
            {
                if (_animator != null)
                    return _animator;

                _animator = AnimRenderer.TryGetAnimator(TargetA.Pawn);
                return _animator;
            }
        }
        private AnimRenderer _animator;

        public virtual bool ShouldContinue => Animator is { IsDestroyed: false };

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return ShouldContinue;
        }

        public override IEnumerable<Toil> MakeNewToils()
        {
            job.collideWithPawns = true;
            job.playerForced = true;
            job.overrideFacing = Rot4.South;

            yield return Toils_General.StopDead();
            yield return MakeWaitToil();
        }

        private Toil MakeWaitToil()
        {
            var toil = ToilMaker.MakeToil();
            toil.handlingFacing = true;
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.AddEndCondition(() =>
            {
                if (ShouldContinue)
                    return JobCondition.Ongoing;
                return JobCondition.Succeeded;
            });
            return toil;
        }

        public override string GetReport()
        {
            return Animator?.Def.description ?? job.def.reportString;
        }
    }
}
