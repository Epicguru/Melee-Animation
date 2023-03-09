using AM;
using AM.Events;
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
            if (index >= 0 && index < Animator.PawnCount)
                return Animator.Pawns[index];
            return null;
        }

        public AnimPartData GetPart(string name) => Animator.GetPart(name);
    }
}
