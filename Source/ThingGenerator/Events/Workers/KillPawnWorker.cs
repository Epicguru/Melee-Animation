using AAM.Patches;
using AAM.UI;
using RimWorld;
using System;
using UnityEngine;
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

            (string outcome, Color color) = animator.ExecutionOutcome switch
            {
                ExecutionOutcome.Damage => ("Injured", Color.yellow),
                ExecutionOutcome.Down => ("Downed", Color.Lerp(Color.yellow, Color.red, 0.5f)),
                ExecutionOutcome.Kill => ("Killed", Color.red),
                _ => (null, default)
            };
            if (outcome != null)
                MoteMaker.ThrowText(pawn.DrawPos + new Vector3(0, 0, 0.6f), pawn.Map, $"Execution Outcome: {outcome}", color);

            Core.Log($"Execution outcome is {animator.ExecutionOutcome}");

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
            var args = new OutcomeUtility.AdditionalArgs()
            {
                BodyPartDef = @event.TargetBodyPart.AsDefOfType<BodyPartDef>(),
                DamageDef = @event.DamageDef.AsDefOfType(DamageDefOf.Cut),
                LogGenDef = @event.BattleLogDef.AsDefOfType(AAM_DefOf.AAM_Execution_Generic),
                Weapon = killer.GetFirstMeleeWeapon(),
                TargetDamageAmount = 20 // TODO pull from animation data, optional args or something along those lines.
            };

            OutcomeUtility.PerformOutcome(ExecutionOutcome.Damage, killer, pawn, args);
        }

        private void Down(AnimEventInput i, Pawn pawn, Pawn killer, KillPawnEvent @event)
        {
            var args = new OutcomeUtility.AdditionalArgs()
            {
                BodyPartDef = @event.TargetBodyPart.AsDefOfType<BodyPartDef>(),
                DamageDef = @event.DamageDef.AsDefOfType(DamageDefOf.Cut),
                LogGenDef = @event.BattleLogDef.AsDefOfType(AAM_DefOf.AAM_Execution_Generic),
                Weapon = killer.GetFirstMeleeWeapon()
            };

            OutcomeUtility.PerformOutcome(ExecutionOutcome.Down, killer, pawn, args);
        }

        private void Kill(AnimEventInput i, Pawn pawn, Pawn killer, KillPawnEvent @event)
        {
            var args = new OutcomeUtility.AdditionalArgs()
            {
                BodyPartDef = @event.TargetBodyPart.AsDefOfType<BodyPartDef>(),
                DamageDef = @event.DamageDef.AsDefOfType(DamageDefOf.Cut),
                LogGenDef = @event.BattleLogDef.AsDefOfType(AAM_DefOf.AAM_Execution_Generic),
                Weapon = killer.GetFirstMeleeWeapon()
            };

            OutcomeUtility.PerformOutcome(ExecutionOutcome.Kill, killer, pawn, args);
        }
    }
}
