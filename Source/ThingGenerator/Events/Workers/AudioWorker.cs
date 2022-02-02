using UnityEngine;
using Verse;
using Verse.Sound;

namespace AAM.Events.Workers
{
    public class AudioWorker : EventWorkerBase
    {
        public override string EventID => "Audio";

        public override void Run(AnimEventInput i)
        {
            var e = i.Event as AudioEvent;

            var def = i.GetDef<SoundDef>(e.AudioDefName);

            if (def == null)
            {
                Core.Error($"Failed to resolve audio def '{e.AudioDefName}'");
                return;
            }

            var realPos = i.Animator.RootTransform.MultiplyPoint3x4(Vector3.zero) + e.LocalPosition;
            var target = new TargetInfo(realPos.ToIntVec3(), i.Animator.Map);
            var info = e.OnCamera ? SoundInfo.OnCamera() : SoundInfo.InMap(target);
            info.volumeFactor = e.VolumeFactor;
            info.pitchFactor = e.PitchFactor;
            if(e.OnCamera)
                info.forcedPlayOnCamera = true;
            def.PlayOneShot(info);
        }
    }
}
