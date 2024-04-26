using AM;
using RimWorld;

namespace AM.Events.Workers
{
    public class TextMoteWorker : EventWorkerBase
    {
        public override string EventID => "TextMote";

        public override void Run(AnimEventInput i)
        {
            var e = i.Event as TextMoteEvent;

            var part = i.GetPart(e.PartName);
            if (part == null)
            {
                Core.Error($"Failed to spawn text mote: could not find animation part '{e.PartName}'");
                return;
            }

            var ss = i.Animator.GetSnapshot(part);
            var pos = ss.GetWorldPosition();
            pos += e.Offset;

            MoteMaker.ThrowText(pos, i.Animator.Map, e.Text, e.Color, e.TimeBeforeFadeStart);
        }
    }
}
