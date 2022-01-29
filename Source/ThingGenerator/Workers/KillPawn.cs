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
            RulePackDef logDef = e.TryParsePart(7, fallback: AAM_DefOf.AAM_Execution_Generic);
            var part = GetPartFromDef(pawn, partDef);
            ThingDef weapon = inst.equipment?.Primary?.def;

            var dInfo = new DamageInfo(dmgDef, 99999, 99999, hitPart: part, instigator: inst, weapon: weapon);
            var log = CreateLog(logDef, inst.equipment?.Primary, inst, pawn);
            dInfo.SetAllowDamagePropagation(false);
            dInfo.SetIgnoreArmor(true);
            dInfo.SetIgnoreInstantKillProtection(true);
            var result = pawn.TakeDamage(dInfo);
            if (!pawn.Dead)
            {
                Find.BattleLog.RemoveEntry(log);
                pawn.Kill(dInfo, result?.hediffs?.FirstOrFallback());
            }
            else
            {
                result.AssociateWithLog(log);
            }

            // Update the pawn wiggler so that the pawn corpse matches the final animation state.
            // This does not change the body position, so when the animation ends and the corpse appears, the corpse often snaps to the center of the cell.
            // I don't know if there is any easy fix for this.
            var animPart = input.Animator.GetPawnBody(pawn);
            if (animPart == null)
                return;

            var bodyRot = input.Animator.GetSnapshot(animPart).GetWorldRotation();
            pawn.Drawer.renderer.wiggler.downedAngle = bodyRot;
        }

        private LogEntry_DamageResult CreateLog(RulePackDef def, Thing weapon, Pawn inst, Pawn vict)
        {
            //var log = new BattleLogEntry_MeleeCombat(rulePackGetter(this.maneuver), alwaysShow, this.CasterPawn, this.currentTarget.Thing, base.ImplementOwnerType, this.tool.labelUsedInLogging ? this.tool.label : "", (base.EquipmentSource == null) ? null : base.EquipmentSource.def, (base.HediffCompSource == null) ? null : base.HediffCompSource.Def, this.maneuver.logEntryDef);
            var log = new BattleLogEntry_MeleeCombat(def, true, inst, vict, ImplementOwnerTypeDefOf.Weapon, weapon?.Label, def: LogEntryDefOf.MeleeAttack);
            Find.BattleLog.Add(log);
            return log;
        }
    }
}
