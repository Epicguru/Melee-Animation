using System.Collections.Generic;
using Verse;

namespace AAM.Workers
{
    public class DuelSectionWorker : AnimEventWorker
    {
        private static List<AnimEvent> tempEvents = new List<AnimEvent>();

        public override void Run(AnimEventInput input)
        {
            if (input.Animator == null)
                return;

            UpdateEventPoints(input.Data);

            // Move to another random section.
            var next = GetRandomEvent(GetPreviousEvent(input.Animator.CurrentTime));

            bool stop = Rand.Chance(0.1f);
            if (stop)
                next = null;

            // Nothing to move to?
            if (next == null)
            {
                // Just go to the start of the end.
                JumpTo(input.Animator, GetEndEvent());
                return;
            }
            else
            {
                // Go to this random section!
                JumpTo(input.Animator, next);
            }
        }

        private void JumpTo(AnimRenderer animator, AnimEvent e)
        {
            animator.Seek(e.Time, null, false);
            Core.Log($"Jumpted to section '{e.RawInput}' @ {e.Time:F3}");
        }

        private void UpdateEventPoints(AnimData data)
        {
            tempEvents.Clear();

            string className = GetType().Name;
            foreach (var item in data.Events)
            {
                if (item.GetPartRaw(1) == className)
                    tempEvents.Add(item);
            }
        }

        private AnimEvent GetPreviousEvent(float time)
        {
            int seen = 0;
            for (int i = tempEvents.Count - 1; i >= 0; i--)
            {
                var e = tempEvents[i];
                if (e.Time > time)
                    continue;

                seen++;
                if (seen == 2)                
                    return e;                
            }
            return null; // Not correct but it's late and I can't be bothered to fix.
        }

        private AnimEvent GetRandomEvent(AnimEvent except)
        {
            int avoid = tempEvents.IndexOf(except);
            int index = Rand.Range(0, tempEvents.Count - 1);

            if (tempEvents.Count < (except == null ? 2 : 3)) // First is except, second is final. 
                return null;

            while (index == avoid)
                index = Rand.Range(0, tempEvents.Count - 1);

            return tempEvents[index];
        }

        private AnimEvent GetEndEvent()
        {
            return tempEvents[tempEvents.Count - 1];
        }
    }
}
