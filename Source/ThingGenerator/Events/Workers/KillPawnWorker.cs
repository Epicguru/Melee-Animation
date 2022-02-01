using AAM.Patches;
using RimWorld;
using Verse;

namespace AAM.Events.Workers
{
    public class KillPawnWorker : EventWorkerBase
    {
        public override string EventID => "KillPawn";

        public override void Run(AnimEventInput i)
        {
            var e = i.Event as KillPawnEvent;
            var animator = i.Animator;
            
            Pawn killer = i.GetPawnFromIndex(e.KillerIndex);
            Pawn pawn = i.GetPawnFromIndex(e.VictimIndex);

            if (pawn == null || pawn.Destroyed || pawn.Dead || killer == null)
                return;

            if (e.OnlyIfNotInterrupted && animator.WasInterrupted)
            {
                Core.Warn($"Anim was interrupted, will not kill {pawn}");
                return;
            }

            BodyPartDef partDef = i.GetDef<BodyPartDef>(e.TargetBodyPart);
            DamageDef dmgDef = i.GetDef(e.DamageDef, DamageDefOf.Cut);
            RulePackDef logDef = i.GetDef(e.BattleLogDef, AAM_DefOf.AAM_Execution_Generic);
            var part = GetPartFromDef(pawn, partDef);
            ThingDef weapon = killer.equipment?.Primary?.def;

            var dInfo = new DamageInfo(dmgDef, 99999, 99999, hitPart: part, instigator: killer, weapon: weapon);
            var log = CreateLog(logDef, killer.equipment?.Primary, killer, pawn);
            dInfo.SetAllowDamagePropagation(false);
            dInfo.SetIgnoreArmor(true);
            dInfo.SetIgnoreInstantKillProtection(true);

            var oldEffecter = pawn.RaceProps?.FleshType?.damageEffecter;
            if (oldEffecter != null)
                pawn.RaceProps.FleshType.damageEffecter = null;

            DamageWorker.DamageResult result;
            try
            {
                result = pawn.TakeDamage(dInfo);
            }
            finally
            {
                if (oldEffecter != null)
                    pawn.RaceProps.FleshType.damageEffecter = oldEffecter;
            }


            if (!pawn.Dead)
            {
                Find.BattleLog.RemoveEntry(log);
                pawn.Kill(dInfo, result?.hediffs?.FirstOrFallback());
            }
            else
            {
                result?.AssociateWithLog(log);
            }

            var animPart = i.Animator.GetPawnBody(pawn);
            if (animPart == null)
                return;

            var ss = i.Animator.GetSnapshot(animPart);

            if (pawn.Corpse != null)
            {
                // Do corpse interpolation - interpolates the corpse to the correct position, after the animated position.
                Patch_Corpse_DrawAt.Interpolators.Add(pawn.Corpse, new CorpseInterpolate(pawn.Corpse, ss.GetWorldPosition()));

                // Corpse facing - make the dead pawn face in the direction that the animation requires.
                bool flipX = i.Animator.MirrorHorizontal;
                if (ss.FlipX)
                    flipX = !flipX;
                
                Patch_PawnRenderer_LayingFacing.OverrideRotations.Add(pawn, flipX ? Rot4.West : Rot4.East); // TODO replace with correct facing once it is animation driven (see patch)
            }
            else
                Core.Warn($"{pawn} did not spawn a corpse after death, or the corpse was destroyed...");

            // Update the pawn wiggler so that the pawn corpse matches the final animation state.
            // This does not change the body position, so when the animation ends and the corpse appears, the corpse often snaps to the center of the cell.
            // I don't know if there is any easy fix for this.
            var bodyRot = ss.GetWorldRotation();
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
