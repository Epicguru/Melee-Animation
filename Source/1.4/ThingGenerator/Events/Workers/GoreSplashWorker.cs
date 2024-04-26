using AM;
using AM.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace AM.Events.Workers
{
    public class GoreSplashWorker : EventWorkerBase
    {
        public override string EventID => "GoreSplash";

        public override void Run(AnimEventInput i)
        {
            if (!Core.Settings.Gore_FloorBlood || Dialog_AnimationDebugger.IsInRehearsalMode)
                return;

            var e = i.Event as GoreSplashEvent;

            var pawn = i.GetPawnFromIndex(e.AroundPawnIndex);
            if (pawn == null)
            {
                Core.Warn("Cannot spawn damage effect for pawn: pawn not found. Attempting to find first pawn.");
                pawn = i.GetPawnFromIndex(0);
                if (pawn == null)
                    return;
            }
            var body = i.GetPawnBody(pawn);

            int count = e.Count;
            float radius = e.Radius;
            var map = pawn.Map;

            var bloodDef = pawn.RaceProps.BloodDef;
            if (bloodDef == null)
            {
                Core.Warn($"Cannot spawn gore for race {pawn.def.label}, no blood def.");
                return;
            }

            var snap = i.Animator.GetSnapshot(body);
            Vector3 basePos = snap.GetWorldPosition();
            for (int j = 0; j < count; j++)
            {
                Vector3 pos = basePos + Rand.InsideUnitCircleVec3 * radius;
                IntVec3 worldPos = pos.ToIntVec3();

                FilthMaker.TryMakeFilth(worldPos, map, bloodDef, pawn.LabelIndefinite(), 1);
            }
        }
    }
}
