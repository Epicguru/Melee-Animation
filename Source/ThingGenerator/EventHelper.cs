using AAM.Workers;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AAM
{
    public static class EventHelper
    {
        private static Dictionary<string, EventHandleFunction> handlers = new Dictionary<string, EventHandleFunction>();

        static EventHelper()
        {
            handlers["mote"] = HandleMote;
            handlers["damageeffect"] = HandleDamageEffector;
            handlers["goresplash"] = HandleGoreSplash;
            handlers["filth"] = HandleFilth;
            handlers["worker"] = HandleWorker;
            handlers["section"] = null; // Ignore sections, they are not real events.
        }

        public static void Handle(AnimEvent e, AnimRenderer animator)
        {
            Core.Log($"Handle: {e}");

            string header = e.HandlerName;
            if (header == null)
                return;

            if (handlers.TryGetValue(e.HandlerName.ToLowerInvariant(), out var func))
            {
                func?.Invoke(e, animator);
            }
            else
            {
                Core.Error($"No handler for animation event of type '{header}': {e}");
            }
        }

        public static void AddHandler(string handle, EventHandleFunction func)
        {
            if (handle == null || func == null)
                return;

            handle = handle.Trim().ToLowerInvariant();
            handlers[handle] = func;
        }

        static void HandleWorker(AnimEvent e, AnimRenderer anim)
        {
            string pos = e.TryParsePart<string>(0);
            var worker = AnimEventWorker.GetWorker(e.TryParsePart<string>(1));

            if (worker == null)
            {
                Core.Error($"Failed to find worker called: '{e.GetPartRaw(1)}'.");
                return;
            }

            switch (pos?.ToLowerInvariant())
            {
                case "now":
                    worker.Run(new AnimEventInput(e, anim, false, null));
                    break;

                case "end":
                    anim.workersAtEnd.Add((e, worker));
                    break;

                default:
                    Core.Error($"Bad time: '{pos}'. Expected Now or End");
                    break;
            }
        }

        static void HandleMote(AnimEvent e, AnimRenderer anim)
        {
            var part = e.TryParsePart<AnimPartData>(0, anim);
            if (part == null)
            {
                Core.Error($"Failed to spawn fleck: could not find animation part '{e.GetPartRaw(0)}'");
                return;
            }

            var fleck = e.TryParsePart<FleckDef>(1);
            if (fleck == null)
            {
                Core.Error($"Failed to spawn fleck: could not find fleck def '{e.GetPartRaw(1)}'");
                return;
            }

            throw new System.NotImplementedException();
        }

        static void HandleDamageEffector(AnimEvent e, AnimRenderer anim)
        {
            var pawn = e.TryParsePart<Pawn>(0, anim);
            if (pawn == null)
            {
                Core.Error($"Cannot spawn damage effect for pawn: pawn not found.");
                return;
            }
            var body = anim.GetPawnBody(pawn);

            EffecterDef damageEffecter = pawn.RaceProps?.FleshType?.damageEffecter;
            if (pawn.health != null && damageEffecter != null)
            {
                if (pawn.health.woundedEffecter != null && pawn.health.woundedEffecter.def != damageEffecter)
                {
                    pawn.health.woundedEffecter.Cleanup();
                }
                Vector3 targetPos = body.CurrentSnapshot.GetWorldPosition(anim.RootTransform);
                IntVec3 basePos = pawn.Position;
                Vector3 offset = targetPos - basePos.ToVector3Shifted();
                pawn.health.woundedEffecter = damageEffecter.Spawn();
                pawn.health.woundedEffecter.offset = offset;
                pawn.health.woundedEffecter.Trigger(pawn, pawn);
            }
        }

        static void HandleGoreSplash(AnimEvent e, AnimRenderer anim)
        {
            var pawn = e.TryParsePart<Pawn>(0, anim);
            if (pawn == null)
            {
                Core.Error($"Cannot spawn damage effect for pawn: pawn not found.");
                return;
            }
            var body = anim.GetPawnBody(pawn);

            int count = e.TryParsePart(1, fallback: 5);
            float radius = e.TryParsePart(2, fallback: 0.5f);
            var map = pawn.Map;
            //Color defaultBlood = pawn.RaceProps.IsMechanoid ? Color.black : pawn.RaceProps.Humanlike ? Color.red : defaultBlood;
            var bloodDef = pawn.RaceProps.BloodDef;
            if (bloodDef == null)
            {
                Core.Warn($"Cannot spawn gore for race {pawn.def.label}, no blood def.");
                return;
            }

            Vector3 basePos = body.CurrentSnapshot.GetWorldPosition(anim.RootTransform);
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = basePos + Rand.InsideUnitCircleVec3 * radius;
                IntVec3 worldPos = pos.ToIntVec3();

                FilthMaker.TryMakeFilth(worldPos, map, bloodDef, pawn.LabelIndefinite(), 1);
            }
        }

        static void HandleFilth(AnimEvent e, AnimRenderer anim)
        {
            throw new System.NotImplementedException();
        }
    }

    public delegate void EventHandleFunction(AnimEvent e, AnimRenderer anim);
}
