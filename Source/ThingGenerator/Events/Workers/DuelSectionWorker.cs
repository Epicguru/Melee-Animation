using AM.Jobs;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AM.Events.Workers
{
    public class DuelSectionWorkerBase : EventWorkerBase
    {
        public override string EventID => "DuelEvent";

        private static readonly List<EventBase> events = new List<EventBase>();

        public override void Run(AnimEventInput input)
        {
            if (input.Animator == null)
                return;

            var saveData = input.Animator.SD;

            UpdateEventPoints(input.Data);

            // Move to another random section.
            var last = GetPreviousEvent(input.Animator.CurrentTime);
            var next = GetRandomEvent(saveData, last);

            // Determine target duration if not done already.
            if (saveData.TargetDuelSegmentCount == 0)
            {
                int min = Core.Settings.MinDuelDuration;
                int max = Core.Settings.MaxDuelDuration;
                
                // Friendly duels are twice as long because it gives other pawns time to spectate.
                if (input.Animator.IsFriendlyDuel)
                {
                    min *= 2;
                    max *= 2;
                }

                if (max < min)
                    max = min;
                saveData.TargetDuelSegmentCount = Rand.RangeInclusive(min, max);
            }

            // End if reached target duration.
            bool end = saveData.DuelSegmentsDoneCount >= saveData.TargetDuelSegmentCount;
            if (end)
                next = null;

            // Nothing to move to?
            if (next == null)
            {
                if (input.Animator.PawnCount < 2)
                {
                    End(input, null, null, ExecutionOutcome.Nothing);
                }
                else
                {
                    // Drum roll...
                    Pawn first = input.Animator.Pawns[0];
                    Pawn second = input.Animator.Pawns[1];
                    float chanceToWin = OutcomeUtility.ChanceToBeatInMelee(first, second);
                    bool firstWins = Rand.Chance(chanceToWin);
                    Pawn winner = firstWins ? first : second;
                    Pawn loser = firstWins ? second : first;
                    var outcome = input.Animator.IsFriendlyDuel ? ExecutionOutcome.Nothing : OutcomeUtility.GenerateRandomOutcome(winner, loser);

                    ThrowMote(winner, loser, firstWins ? chanceToWin : 1f - chanceToWin);
                    End(input, winner, loser, outcome);
                }
            }
            else
            {
                // Go to this random section!
                JumpTo(input.Animator, next);
                saveData.DuelSegmentsDoneCount++;
                saveData.DuelSegmentsDone.Add(events.IndexOf(next));
            }
        }

        private void ThrowMote(Pawn winner, Pawn loser, float chance)
        {
            int loserAlignment = GetPawnAlignment(loser);
            Color color = loserAlignment switch
            {
                < 0 => Color.green,
                0 => Color.yellow,
                > 0 => Color.red,
            };
            MoteMaker.ThrowText(winner.DrawPos + new Vector3(0, 0, 1), winner.Map, $"{winner.Name.ToStringShort} wins! ({chance * 100f:F0}% chance)", color);

        }

        /// <summary>
        /// 0 is neutral, 1 is friendly, -1 is hostile.
        /// </summary>
        private int GetPawnAlignment(Pawn pawn)
        {
            if (pawn.IsColonist || pawn.IsSlaveOfColony)
                return 1;

            if (pawn.HostileTo(Faction.OfPlayerSilentFail))
                return -1;

            return 0;
        }

        private void End(in AnimEventInput input, Pawn winner, Pawn loser, ExecutionOutcome outcome)
        {
            if (winner == null)
            {
                // Just go to the start of the end.
                JumpTo(input.Animator, GetEndEvent());
                return;
            }

            // Notify jobs if they want it.
            if (winner.jobs?.curDriver is IDuelEndNotificationReceiver r)
                r.Notify_OnDuelEnd(true);
            if (loser?.jobs?.curDriver is IDuelEndNotificationReceiver r2)
                r2.Notify_OnDuelEnd(false);

            // Perform an execution animation with the outcome.
            var jobDef = input.Animator.CustomJobDef;
            input.Animator.OnEndAction = a =>
            {
                StartEndExecution(winner, a.Pawns.FirstOrDefault(p => p != null && p != winner), outcome, jobDef);
            };
            input.Animator.Destroy();
        }

        private static AnimDef GetWinAnimation(Pawn winner, Pawn loser, ExecutionOutcome outcome)
        {
            // Check if it's a friendly duel.
            if (outcome == ExecutionOutcome.Nothing)
            {
                return AM_DefOf.AM_Duel_WinFriendlyDuel;
            }

            // List all possible execution animations.
            var allAnims = AnimDef.GetExecutionAnimationsForPawnAndWeapon(winner, winner.GetFirstMeleeWeapon()?.def).ToList();

            // Make a space mask.
            bool flipX = winner.Position.x > loser.Position.x;
            ulong mask = SpaceChecker.MakeOccupiedMask(winner.Map, winner.Position, out _);
            AnimDef anim = null;

            for (int i = 0; i < 1000; i++)
            {
                var rand = allAnims.RandomElementByWeightWithFallback(d => d.Probability);
                if (rand == null)
                {
                    Core.Error($"No possible execution animations for {winner} using {winner.GetFirstMeleeWeapon()}. End of duel will not go as planned...");
                    return null;
                }

                // Check space.
                ulong animMask = flipX ? rand.FlipClearMask : rand.ClearMask;
                if ((mask & animMask) != 0)
                {
                    // Cannot be performed, no space.
                    allAnims.Remove(rand);
                    continue;
                }

                // Valid!
                anim = rand;
                break;
            }

            return anim;
        }

        private static void StartEndExecution(Pawn winner, Pawn loser, ExecutionOutcome outcome, JobDef customJobDef)
        {
            var anim = GetWinAnimation(winner, loser, outcome);
            bool flipX = winner.Position.x > loser.Position.x;

            if (anim == null)
            {
                Core.Error("No execution animation can be performed at end of duel! Probably no space/skill for any of them!");
                return;
            }

            var startArgs = new AnimationStartParameters(anim, winner, loser)
            {
                ExecutionOutcome = outcome,
                FlipX = flipX,
                CustomJobDef = customJobDef
            };

            if (!startArgs.TryTrigger())
                Core.Error("Failed to start end of duel execution animation.");
        }

        private void JumpTo(AnimRenderer animator, EventBase e)
        {
            animator.Seek(e.Time, 0, null);
        }

        private void UpdateEventPoints(AnimData data)
        {
            events.Clear();
            foreach (var item in data.Events)
            {
                if (item.EventID == EventID)
                    events.Add(item);
            }
        }

        private EventBase GetPreviousEvent(float time)
        {
            int seen = 0;
            for (int i = events.Count - 1; i >= 0; i--)
            {
                var e = events[i];
                if (e.Time > time)
                    continue;

                seen++;
                if (seen == 2)
                    return e;
            }
            return null;
        }

        private EventBase GetRandomEvent(AnimRenderer.SaveData sd, EventBase last)
        {
            int totalCount = events.Count - 1;
            int done = sd.DuelSegmentsDone.Count;

            EventBase RandomWhere(Predicate<EventBase> allowed)
            {
                for (int i = 0; i < 100; i++)
                {
                    var rand = events[Rand.Range(0, events.Count - 1)];
                    if (allowed(rand))
                        return rand;
                }
                Core.Error("Failed to select a random duel event, predicate excluded all?");
                return null;
            }

            if (done >= totalCount)
            {
                // Already played all duel animations, just pick a random one again (except the one that was just played).
                return RandomWhere(e => e != last);
            }

            // Pick a random event that hasn't been played before.
            return RandomWhere(e => !sd.DuelSegmentsDone.Contains(events.IndexOf(e)));
        }

        private EventBase GetEndEvent()
        {
            return events[events.Count - 1];
        }
    }
}
