using Verse;

namespace AM.Events.Workers
{
    public struct AnimEventInput
    {
        public AnimData Data => Animator?.Data;
        public EventBase Event;
        public AnimRenderer Animator;

        public AnimEventInput(EventBase e, AnimRenderer renderer)
        {
            Event = e;
            Animator = renderer;
        }

        public AnimPartData GetPawnBody(int index) => Animator.GetPawnBody(GetPawnFromIndex(index));

        public AnimPartData GetPawnBody(Pawn pawn) => Animator.GetPawnBody(pawn);

        public Pawn GetPawnFromIndex(int index)
        {
            switch (index)
            {
                case < 0:
                    index = -index - 1;
                    return index < Animator.NonAnimatedPawns.Count ? Animator.NonAnimatedPawns[index] : null;

                case >= 0 when index < Animator.PawnCount:
                    return Animator.Pawns[index];

                default:
                    return null;
            }
        }

        public AnimPartData GetPart(string name) => Animator.GetPart(name);
    }
}
