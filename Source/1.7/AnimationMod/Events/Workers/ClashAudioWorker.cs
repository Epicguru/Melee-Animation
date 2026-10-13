using UnityEngine;
using Verse;
using Verse.Sound;

namespace AM.Events.Workers
{
    public class ClashAudioWorker : EventWorkerBase
    {
        public override string EventID => "WeaponClash";

        public override void Run(AnimEventInput i)
        {
            if (Core.Settings.DuelVolumePct <= 0)
            {
                return;
            }
            
            var weapon1 = i.GetPawnFromIndex(0)?.GetFirstMeleeWeapon();
            var weapon2 = i.GetPawnFromIndex(1)?.GetFirstMeleeWeapon();

            var sound = AudioUtility.GetWeaponClashSound(weapon1, weapon2);
            if (sound == null)
                return;

            var pos = i.Animator.RootTransform.MultiplyPoint3x4(Vector3.zero);
            
            var soundInfo = SoundInfo.InMap(new TargetInfo(new IntVec3(pos), i.Animator.Map));
            soundInfo.volumeFactor = Core.Settings.DuelVolumePct;
            
            sound.PlayOneShot(soundInfo);
        }
    }
}
