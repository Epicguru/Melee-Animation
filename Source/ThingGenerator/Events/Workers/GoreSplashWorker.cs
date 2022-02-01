using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace AAM.Events.Workers
{
    public class GoreSplashWorker : EventWorkerBase
    {
        public override string EventID => "GoreSplash";

        public override void Run(AnimEventInput i)
        {
            var e = i.Event as GoreSplashEvent;

            var pawn = i.GetPawnFromIndex(e.AroundPawnIndex);
            if (pawn == null)
            {
                Core.Error("Cannot spawn damage effect for pawn: pawn not found.");
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
