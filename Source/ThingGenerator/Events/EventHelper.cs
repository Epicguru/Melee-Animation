using AAM.Events.Workers;

namespace AAM.Events
{
    public static class EventHelper
    {
        public static void Handle(EventBase e, AnimRenderer animator)
        {
            var worker = e.GetWorker<EventWorkerBase>();
            if (worker == null)
            {
                Core.Warn($"There is no worker to handle event '{e.EventID}'");
                return;
            }

            if (e is TimedEvent te)
            {
                switch (te.When)
                {
                    case EventTime.Now:
                        break;
                    case EventTime.AtEnd:
                        animator.workersAtEnd.Add((e, worker));
                        return;
                    default:
                        Core.Error($"Unhandled EventTime: {te.When}");
                        return;

                }
            }
            worker.Run(new AnimEventInput(e, animator, false, null));
        }
    }
}
