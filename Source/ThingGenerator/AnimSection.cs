using AAM.Workers;
using System;

namespace AAM
{
    public class AnimSection
    {
        public readonly AnimData Data;
        public readonly AnimEvent StartEvent, EndEvent;
        public readonly string Name;
        public readonly float StartTime, EndTime;
        public readonly bool IsStart, IsEnd;
        public readonly AnimEventWorker Worker;

        public AnimSection(AnimData data, AnimEvent startEvent, AnimEvent endEvent)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));

            StartEvent = startEvent;
            EndEvent = endEvent;

            if (startEvent == null && endEvent == null)
            {
                Name = "_Full";
                IsStart = true;
                IsEnd = true;
                StartTime = 0;
                EndTime = data.Duration;
                Worker = null;
            }
            else if (startEvent == null)
            {
                Name = "_Start";
                IsStart = true;
                StartTime = 0;
                EndTime = endEvent.Time;
                Worker = MakeWorker(endEvent);
            }
            else if (endEvent == null)
            {
                Name = "_End";
                IsEnd = true;
                StartTime = startEvent.Time;
                EndTime = data.Duration;
                Worker = MakeWorker(startEvent);
                Name = startEvent.GetPartRaw(1);
            }
            else
            {
                Name = startEvent.GetPartRaw(1);
                StartTime = startEvent.Time;
                EndTime = endEvent.Time;
                Worker = MakeWorker(startEvent);
            }
        }

        public virtual void OnSectionEnter(AnimRenderer renderer)
        {
            Worker?.Run(new AnimEventInput(StartEvent, renderer, true, this));
        }

        public virtual void OnSectionExit(AnimRenderer renderer)
        {
            Worker?.Run(new AnimEventInput(EndEvent, renderer, false, this));
        }

        public virtual AnimEventWorker MakeWorker(AnimEvent e)
        {
            if (e == null)
                return null;
            return AnimEventWorker.GetWorker(e.GetPartRaw(0));
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
