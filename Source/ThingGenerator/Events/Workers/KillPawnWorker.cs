using AAM.Patches;
using AAM.UI;
using RimWorld;
using System;
using Verse;

namespace AAM.Events.Workers
{
    public class KillPawnWorker : EventWorkerBase
    {
        public override string EventID => "KillPawn";

        public override void Run(AnimEventInput i)
        {
            var animator = i.Animator;

            if (Dialog_AnimationDebugger.IsInRehearsalMode)
            {
                if (animator.ExecutionOutcome >= ExecutionOutcome.Down)
                    animator.Destroy();
                return;
            }

            var e = i.Event as KillPawnEvent;
            if (animator.ExecutionOutcome == ExecutionOutcome.Nothing)
                return;
            
            Pawn killer = i.GetPawnFromIndex(e.KillerIndex);
            Pawn pawn = i.GetPawnFromIndex(e.VictimIndex);

            if (pawn == null || pawn.Destroyed || pawn.Dead || killer == null)
                return;

            switch (animator.ExecutionOutcome)
            {
                case ExecutionOutcome.Nothing:
                    return;

                case ExecutionOutcome.Damage:
                    Injure(i, pawn, killer, e);
                    if (pawn.Downed || pawn.Dead)
                    {
                        Core.Warn($"Injuring {pawn} in execution animation (in Damage mode) caused them to be downed, even though it was not the intended outcome...");
                        animator.Destroy();
                    }
                    break;

                case ExecutionOutcome.Down:
                    Down(i, pawn, killer, e);
                    animator.Destroy();
                    break;

                case ExecutionOutcome.Kill:
                    Kill(i, pawn, killer, e);
                    animator.Destroy();
                    return;

                default:
                    throw new ArgumentOutOfRangeException();
            }


            // End the animation because the victim has been killed.
            // If the animation is not ended, it will continue with the 'recovery' part of the animation,
            // with the victim standing back up.
        }

        private void Injure(AnimEventInput i, Pawn pawn, Pawn killer, KillPawnEvent @event)
        {
            // TODO implement me.
            Core.Warn("IMPLEMENT ME");
        }

        private void Down(AnimEventInput i, Pawn pawn, Pawn killer, KillPawnEvent @event)
        {
            // TODO implement me.
            Core.Warn("IMPLEMENT ME");
        }

        private void Kill(AnimEventInput i, Pawn pawn, Pawn killer, KillPawnEvent @event)
        {
            if (Core.Settings.ExecutionsCanDestroyBodyParts)
            {
                BodyPartDef partDef = i.GetDef<BodyPartDef>(@event.TargetBodyPart);
                DamageDef dmgDef = i.GetDef(@event.DamageDef, DamageDefOf.Cut);
                RulePackDef logDef = i.GetDef(@event.BattleLogDef, AAM_DefOf.AAM_Execution_Generic);
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
            }
            else
            {
                // Magic kill...
                BodyPartDef partDef = i.GetDef<BodyPartDef>(@event.TargetBodyPart);
                DamageDef dmgDef = i.GetDef(@event.DamageDef, DamageDefOf.Cut);
                var part = GetPartFromDef(pawn, partDef);
                ThingDef weapon = killer.equipment?.Primary?.def;

                // Does 0.01 damage, kills anyway.
                var dInfo = new DamageInfo(dmgDef, 0.01f, 0f, hitPart: part, instigator: killer, weapon: weapon);
                pawn.Kill(dInfo);
            }

            var animPart = i.Animator.GetPawnBody(pawn);
            if (animPart == null)
                return;

            var ss = i.Animator.GetSnapshot(animPart);

            if (pawn.Corpse != null)
            {
                // Do corpse interpolation - interpolates the corpse to the correct position, after the animated position.
                Patch_Corpse_DrawAt.Interpolators[pawn.Corpse] = new CorpseInterpolate(pawn.Corpse, ss.GetWorldPosition());

                Patch_PawnRenderer_LayingFacing.OverrideRotations[pawn] = ss.GetWorldDirection();
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
