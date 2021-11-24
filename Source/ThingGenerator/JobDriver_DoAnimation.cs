using System.Collections.Generic;
using Verse.AI;

namespace AAM
{
    public class JobDriver_DoAnimation : JobDriver
    {
        public AnimRenderer Animator
        {
            get
            {
                if (_animator != null)
                    return _animator;

                _animator = AnimRenderer.TryGetAnimator(pawn);
                return _animator;
            }
        }

        public virtual bool ShouldContinue => Animator != null && !Animator.Destroyed;

        private AnimRenderer _animator;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return ShouldContinue;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var toil = new Toil();
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.tickAction = () => { };
            toil.AddEndCondition(() =>
            {
                if (ShouldContinue)
                    return JobCondition.Ongoing;
                return JobCondition.Succeeded;
            });
            yield return toil;
        }

        public override string GetReport()
        {
            return base.GetReport() + $"Remaining: {ticksLeftThisToil} ({CurToil})";
        }
    }
}
