using Verse;

namespace AAM.Events.Workers
{
    public struct AnimEventInput
    {
        public AnimData Data => Animator?.Data;
        public EventBase Event;
        public AnimRenderer Animator;
        public bool IsSectionStart;
        public AnimSection Section;

        public AnimEventInput(EventBase e, AnimRenderer renderer, bool isSectionStart, AnimSection section)
        {
            Event = e;
            Animator = renderer;
            IsSectionStart = isSectionStart;
            Section = section;
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

        public T GetDef<T>(string defName, T fallback = null) where T : Def
        {
            if (string.IsNullOrWhiteSpace(defName))
                return fallback;

            return (T)GenGeneric.InvokeStaticMethodOnGenericType(typeof(DefDatabase<>), typeof(T), "GetNamed", defName, true) ?? fallback;
        }
    }
}
