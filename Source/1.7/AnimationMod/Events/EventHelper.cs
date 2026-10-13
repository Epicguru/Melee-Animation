using AM.Events.Workers;

namespace AM.Events;

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

        worker.Run(new AnimEventInput(e, animator));
    }
}