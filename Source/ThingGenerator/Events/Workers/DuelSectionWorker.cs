using System.Collections.Generic;
using Verse;

namespace AAM.Events.Workers
{
    public class DuelSectionWorkerBase : EventWorkerBase
    {
        public override string EventID => "DuelEvent";

        private static readonly List<EventBase> tempEvents = new();

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

        private void JumpTo(AnimRenderer animator, EventBase e)
        {
            animator.Seek(e.Time, null);
        }

        private void UpdateEventPoints(AnimData data)
        {
            tempEvents.Clear();

            foreach (var item in data.Events)
            {
                if (item.EventID == EventID)
                    tempEvents.Add(item);
            }
        }

        private EventBase GetPreviousEvent(float time)
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

        private EventBase GetRandomEvent(EventBase except)
        {
            int avoid = tempEvents.IndexOf(except);
            int index = Rand.Range(0, tempEvents.Count - 1);

            if (tempEvents.Count < (except == null ? 2 : 3)) // First is except, second is final. 
                return null;

            while (index == avoid)
                index = Rand.Range(0, tempEvents.Count - 1);

            return tempEvents[index];
        }

        private EventBase GetEndEvent()
        {
            return tempEvents[tempEvents.Count - 1];
        }
    }
}
