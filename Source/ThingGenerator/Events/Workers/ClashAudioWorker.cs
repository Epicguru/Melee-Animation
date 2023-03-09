using AM;
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
            var weapon1 = i.GetPawnFromIndex(0)?.GetFirstMeleeWeapon();
            var weapon2 = i.GetPawnFromIndex(1)?.GetFirstMeleeWeapon();

            var sound = AudioUtility.GetWeaponClashSound(weapon1, weapon2);
            if (sound == null)
                return;

            var pos = i.Animator.RootTransform.MultiplyPoint3x4(Vector3.zero);
            sound.PlayOneShot(SoundInfo.InMap(new TargetInfo(new IntVec3(pos), i.Animator.Map)));
        }
    }
}
