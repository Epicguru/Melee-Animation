using UnityEngine;
using Verse;
using Verse.Sound;

namespace AM.Events.Workers
{
    public class DamageEffectWorker : EventWorkerBase
    {
        public override string EventID => "DamageEffect";

        public override void Run(AnimEventInput i)
        {
            var e = i.Event as DamageEffectEvent;
            var pawn = i.GetPawnFromIndex(e.PawnIndex);
            if (pawn == null)
            {
                Core.Warn("Cannot spawn damage effect for pawn: pawn not found. Attempting to find default pawn...");
                pawn = i.GetPawnFromIndex(0);
                if (pawn == null)
                    return;
            }
            var body = i.GetPawnBody(pawn);

            PlayImpactSound(pawn.GetFirstMeleeWeapon(), pawn.Map, pawn.Position);

            if (!Core.Settings.Gore_DamageEffect)
                return;

            EffecterDef damageEffecter = pawn.RaceProps?.FleshType?.damageEffecter;
            if (pawn.health != null && damageEffecter != null)
            {
                if (pawn.health.woundedEffecter != null && pawn.health.woundedEffecter.def != damageEffecter)
                {
                    pawn.health.woundedEffecter.Cleanup();
                }
                var snap = i.Animator.GetSnapshot(body);
                Vector3 targetPos = snap.GetWorldPosition();
                IntVec3 basePos = pawn.Position;
                Vector3 offset = targetPos - basePos.ToVector3Shifted();
                pawn.health.woundedEffecter = damageEffecter.Spawn();
                pawn.health.woundedEffecter.offset = offset;
                pawn.health.woundedEffecter.Trigger(pawn, pawn);
            }
        }

        private void PlayImpactSound(Thing weapon, Map map, IntVec3 position)
        {
            var sd = AudioUtility.GetPawnHitSound(weapon);
            if (sd == null)
                return;

            sd.PlayOneShot(SoundInfo.InMap(new TargetInfo(position, map)));
        }
    }
}
