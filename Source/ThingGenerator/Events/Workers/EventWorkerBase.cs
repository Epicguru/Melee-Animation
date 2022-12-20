using System;
using System.Collections.Generic;
using Verse;

namespace AAM.Events.Workers
{
    public abstract class EventWorkerBase
    {
        public static EventWorkerBase GetWorker(string forID)
        {
            if (allWorkers == null)
            {
                // Load them all.
                allWorkers = new Dictionary<string, EventWorkerBase>();

                foreach (var c in typeof(EventWorkerBase).AllSubclassesNonAbstract())
                {
                    try
                    {
                        var instance = Activator.CreateInstance(c) as EventWorkerBase;
                        allWorkers.Add(instance.EventID, instance);
                    }
                    catch (Exception e)
                    {
                        Core.Error($"Failed to create animation event worker for class '{c.FullName}'", e);
                    }
                }
            }

            if (forID == null)
                return null;

            if (allWorkers.TryGetValue(forID, out var found))
                return found;

            return null;
        }

        private static Dictionary<string, EventWorkerBase> allWorkers;

        public abstract string EventID { get; }

        public abstract void Run(AnimEventInput input);
    }
}
