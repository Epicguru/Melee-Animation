using System.Collections.Generic;
using Verse;
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

        public virtual bool ShouldContinue => Animator != null && !Animator.IsDestroyed;

        private AnimRenderer _animator;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return ShouldContinue;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            job.collideWithPawns = true;
            job.playerForced = true;

            var toil = new Toil();
            toil.handlingFacing = true;
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.tickAction = () =>
            {
                // TODO FROM ANIMATION ROTATION.
                //job.overrideFacing = ;
            };
            toil.AddEndCondition(() =>
            {
                if (ShouldContinue)
                    return JobCondition.Ongoing;
                return JobCondition.Succeeded;
            });
            yield return toil;
        }

        public Pawn GetFirstPawnNotSelf()
        {
            foreach (var p in Animator.Pawns)
            {
                if (p == null || p == pawn)
                    continue;
                return p;
            }
            return null;
        }

        public virtual string ProcessReport(string input)
        {
            if (input == null)
                return "<unnamed>";

            if (Animator == null)
                return input;

            string other = GetFirstPawnNotSelf()?.NameShortColored ?? "???";
            input = input.Replace("[OTHER]", other);

            return input;
        }

        public override string GetReport()
        {
            return $"Animation: {ProcessReport(Animator?.Def?.jobString)}";
        }
    }
}
