using RimWorld;
using Verse;

namespace AAM.Workers
{
    public class KillPawn : AnimEventWorker
    {
        public override void Run(AnimEventInput input)
        {
            var e = input.Event;
            var animator = input.Animator;

            Pawn inst = e.TryParsePart<Pawn>(2, animator);
            Pawn pawn = e.TryParsePart<Pawn>(3, animator);
            if (pawn == null || pawn.Destroyed || pawn.Dead || inst == null)
                return;

            bool onlyNotInterrupted = e.TryParsePart(4, fallback: true);
            if (onlyNotInterrupted && animator.WasInterrupted)
            {
                Core.Warn("Anim was interrupted, will not kill");
                return;
            }

            BodyPartDef partDef = e.TryParsePart<BodyPartDef>(5);
            DamageDef dmgDef = e.TryParsePart(6, fallback: DamageDefOf.Cut);
            var part = GetPartFromDef(pawn, partDef);
            ThingDef weapon = inst.equipment?.Primary?.def;
            Core.Log($"[EXECUTION] Hitting part '{partDef}' ({part}) using {dmgDef}. Attacker is {inst} using {weapon}");

            var dInfo = new DamageInfo(dmgDef, 999999, 99999, hitPart: part, instigator: inst, weapon: weapon);
            dInfo.SetAllowDamagePropagation(false);
            dInfo.SetIgnoreArmor(true);
            dInfo.SetIgnoreInstantKillProtection(true);
            var result = pawn.TakeDamage(dInfo);
            if (!pawn.Dead)
                pawn.Kill(dInfo, result?.hediffs?.FirstOrFallback());
        }
    }
}
