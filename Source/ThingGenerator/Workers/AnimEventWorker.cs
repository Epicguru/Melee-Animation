using System;
using System.Collections.Generic;
using Verse;

namespace AAM.Workers
{
    public abstract class AnimEventWorker
    {
        public static AnimEventWorker GetWorker(string className)
        {
            if (allWorkers == null)
            {
                // Load them all.
                allWorkers = new Dictionary<string, AnimEventWorker>();

                foreach (var c in typeof(AnimEventWorker).AllSubclassesNonAbstract())
                {
                    try
                    {
                        var instance = Activator.CreateInstance(c) as AnimEventWorker;
                        allWorkers.Add(c.Name, instance);
                    }
                    catch (Exception e)
                    {
                        Core.Error($"Failed to create animation event worker for class '{c.FullName}': probably missing no-args constructor...", e);
                    }
                }
            }

            if (className == null)
                return null;

            if (allWorkers.TryGetValue(className, out var found))
                return found;
            return null;
        }

        private static Dictionary<string, AnimEventWorker> allWorkers;

        public abstract void Run(AnimEventInput input);

        public BodyPartRecord GetPartFromDef(Pawn pawn, BodyPartDef def)
        {
            if (def == null)
                return null;
            if (pawn?.health?.hediffSet == null)
                return null;

            foreach (BodyPartRecord bodyPartRecord in pawn.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Undefined, null, null))
            {
                if (bodyPartRecord.def == def)
                    return bodyPartRecord;
            }
            return null;
        }
    }

    public struct AnimEventInput
    {
        public AnimData Data => Animator?.Data;
        public AnimEvent Event;
        public AnimRenderer Animator;
        public bool IsSectionStart;
        public AnimSection Section;

        public AnimEventInput(AnimEvent e, AnimRenderer renderer, bool isSectionStart, AnimSection section)
        {
            Event = e;
            Animator = renderer;
            IsSectionStart = isSectionStart;
            Section = section;
        }
    }
}
