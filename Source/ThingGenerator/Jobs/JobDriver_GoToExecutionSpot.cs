using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace AM.Jobs;

public class JobDriver_GoToExecutionSpot : JobDriver_GoToAnimationSpot
{
    private readonly HashSet<AnimDef> except = new HashSet<AnimDef>();

    protected override Toil MakeEndToil()
    {
#if V13
        var toil = new Toil();
#else
        var toil = ToilMaker.MakeToil();
#endif
        // Try do an execution animation.
        toil.initAction = () =>
        {
            if (Target == null)
                return;

            // Position and flip status.
            IntVec3 targetPos = Target.Position;
            bool flipX = targetPos.x < pawn.Position.x;

            // Get weapon, possible animations, and space mask.
            var weaponDef = pawn.GetFirstMeleeWeapon().def;
            var possibilities = AnimDef.GetExecutionAnimationsForPawnAndWeapon(pawn, weaponDef);
            ulong occupiedMask = SpaceChecker.MakeOccupiedMask(pawn.Map, pawn.Position, out _);

            if (!possibilities.Any())
            {
                Core.Warn("No possible execution animations after reaching target (none by weapon/pawn)!");
                toil.actor.jobs.curDriver.EndJobWith(JobCondition.Errored);
                return;
            }

            // Check space...
            except.Clear();
            while (true)
            {
                // Pick random anim, weighted.
                var anim = possibilities.RandomElementByWeightExcept(d => d.Probability, except);
                if (anim == null)
                {
                    Core.Warn("No possible execution animations after reaching target (no space)!");
                    return;
                }

                except.Add(anim);

                // Do we have space for this animation?
                ulong animMask = flipX ? anim.FlipClearMask : anim.ClearMask;
                ulong result = animMask & occupiedMask; // The result should be 0.

                if (result == 0)
                {
                    // Can do the animation!
                    var args = new AnimationStartParameters(anim, toil.actor, Target)
                    {
                        ExecutionOutcome = OutcomeUtility.GenerateRandomOutcome(toil.actor, Target),
                        FlipX = flipX,
                    };

                    // Trigger the execution animation.
                    bool worked = args.TryTrigger();
                    if (worked)
                    {
                        // Reset execution cooldown.
                        toil.actor.GetMeleeData().TimeSinceExecuted = 0;
                    }
                    else
                    {
                        Core.Warn($"Failed to trigger {anim} on {Target}, possibly invalid pawn or in another animation?");
                        toil.actor.jobs.curDriver.EndJobWith(JobCondition.Errored);
                        return;
                    }

                    // Finished.
                    return;
                }
            }
        };

        toil.defaultCompleteMode = ToilCompleteMode.Instant;

        return toil;
    }
}