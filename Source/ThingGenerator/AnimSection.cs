using AAM.Events;
using AAM.Events.Workers;
using System;

namespace AAM
{
    public class AnimSection
    {
        public readonly AnimData Data;
        public readonly EventBase StartEvent, EndEvent;
        public readonly string Name;
        public readonly float StartTime, EndTime;
        public readonly bool IsStart, IsEnd;
        public readonly EventWorkerBase WorkerBase;

        public AnimSection(AnimData data, EventBase startEvent, EventBase endEvent)
        {
            throw new NotImplementedException();
        }

        public virtual void OnSectionEnter(AnimRenderer renderer)
        {
            WorkerBase?.Run(new AnimEventInput(StartEvent, renderer, true, this));
        }

        public virtual void OnSectionExit(AnimRenderer renderer)
        {
            WorkerBase?.Run(new AnimEventInput(EndEvent, renderer, false, this));
        }

        public virtual EventWorkerBase MakeWorker(EventBase e)
        {
            throw new NotImplementedException();
        }

        public bool ContainsTime(float time)
        {
            if (time == 0)
                return StartTime == 0;

            if (time == Data.Duration)
                return EndTime == Data.Duration;

            return time > StartTime && time <= EndTime;
        }
    }
}
