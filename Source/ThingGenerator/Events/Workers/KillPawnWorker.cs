using AM.UI;
using RimWorld;
using System;
using UnityEngine;
using Verse;

namespace AM.Events.Workers
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

            if (Core.Settings.ShowExecutionMotes)
            {
                (string outcome, Color color) = animator.ExecutionOutcome switch
                {
                    ExecutionOutcome.Damage => ("Injured", Color.yellow),
                    ExecutionOutcome.Down => ("Downed", Color.Lerp(Color.yellow, Color.magenta, 0.35f)),
                    ExecutionOutcome.Kill => ("Killed", Color.Lerp(Color.white, Color.red, 0.7f)),
                    _ => (null, default)
                };
                if (outcome != null)
                    MoteMaker.ThrowText(pawn.DrawPos + new Vector3(0, 0, 0.6f), pawn.Map, $"Execution Outcome: {outcome}", color);
            }

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
                    animator.Destroy();
                    Down(i, pawn, killer, e);
                    break;

                case ExecutionOutcome.Kill:
                    animator.Destroy();
                    Kill(i, pawn, killer, e);
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
                LogGenDef = @event.BattleLogDef.AsDefOfType(AM_DefOf.AM_Execution_Generic),
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
                LogGenDef = @event.BattleLogDef.AsDefOfType(AM_DefOf.AM_Execution_Generic),
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
                LogGenDef = @event.BattleLogDef.AsDefOfType(AM_DefOf.AM_Execution_Generic),
                Weapon = killer.GetFirstMeleeWeapon()
            };

            OutcomeUtility.PerformOutcome(ExecutionOutcome.Kill, killer, pawn, args);
        }
    }
}
