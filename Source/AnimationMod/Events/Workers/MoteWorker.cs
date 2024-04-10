using AM;
using RimWorld;
using Verse;

namespace AM.Events.Workers
{
    public class MoteWorker : EventWorkerBase
    {
        public override string EventID => "Mote";

        public override void Run(AnimEventInput i)
        {
            var e = i.Event as MoteEvent;

            var part = i.GetPart(e.PartName);
            if (part == null)
            {
                Core.Error($"Failed to spawn fleck: could not find animation part '{e.PartName}'");
                return;
            }

            var fleck = e.MoteDef.AsDefOfType<FleckDef>();
            if (fleck == null)
            {
                Core.Error($"Failed to spawn fleck: could not find fleck def '{e.MoteDef}'");
                return;
            }

            var map = i.Animator.Map;
            var partSS = part.GetSnapshot(i.Animator);
            var loc = partSS.GetWorldPosition();
            bool shouldMirror = i.Animator.MirrorHorizontal;

            for (int j = 0; j < 20; j++)
            {
                if (!loc.ToIntVec3().ShouldSpawnMotesAt(map))
                {
                    return;
                }

                float baseAngle = e.StartVelocityAngle.RandomInRange();
                float angle = shouldMirror ? baseAngle - 90 : 90 - baseAngle;

                FleckCreationData dataStatic = FleckMaker.GetDataStatic(loc + e.WithOffset, map, fleck, e.StartScale.RandomInRange());
                dataStatic.rotationRate = e.StartRotationSpeed.RandomInRange();
                dataStatic.velocityAngle = angle;
                dataStatic.velocitySpeed = e.StartVelocityMagnitude.RandomInRange();
                dataStatic.solidTimeOverride = 0.1f;
                map.flecks.CreateFleck(dataStatic);
            }
        }
    }
}
