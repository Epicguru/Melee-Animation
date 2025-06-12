using System.Collections.Generic;
using System.Linq;
using AM.Outcome;
using AM.Reqs;
using Verse;
using Verse.AI;

namespace AM.Jobs;

public class JobDriver_GoToExecutionSpot : JobDriver_GoToAnimationSpot
{
    public static IEnumerable<AnimDef> UseTheseAnimations;

    public List<AnimDef> OnlyTheseAnimations = new List<AnimDef>();

    private readonly HashSet<AnimDef> except = new HashSet<AnimDef>();

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Collections.Look(ref OnlyTheseAnimations, "onlyTheseAnimations", LookMode.Def);
    }

    public override IEnumerable<Toil> MakeNewToils()
    {
        if (UseTheseAnimations != null)
        {
            OnlyTheseAnimations ??= new List<AnimDef>();
            OnlyTheseAnimations.AddRange(UseTheseAnimations);
        }

        return base.MakeNewToils();
    }

    protected override Toil MakeEndToil()
    {
        var toil = ToilMaker.MakeToil();
        
        // Try do an execution animation.
        toil.initAction = () =>
        {
            if (Target == null)
            {
                Core.Error("Null target in toil creation.");
                return;
            }

            // Position and flip status.
            IntVec3 targetPos = Target.Position;
            bool flipX = targetPos.x < pawn.Position.x;

            // Get weapon, possible animations, and space mask.
            var weaponDef = pawn.GetFirstMeleeWeapon()?.def;
            bool fixedAnimationList = OnlyTheseAnimations is { Count: > 0 };
            var possibilities = fixedAnimationList ? OnlyTheseAnimations : AnimDef.GetExecutionAnimationsForPawnAndWeapon(pawn, weaponDef);
            ulong occupiedMask = SpaceChecker.MakeOccupiedMask(pawn.Map, pawn.Position, out _);

            if (!possibilities.Any())
            {
                Core.Warn("No possible execution animations after reaching target (none by weapon/pawn)!");
                toil.actor.jobs.curDriver.EndJobWith(JobCondition.Errored);
                return;
            }

            var outcome = OutcomeUtility.GenerateRandomOutcome(toil.actor, Target, true);

            // Check space...
            bool failDoubleLoop = false;
            except.Clear();
            while (true)
            {
                AnimDef anim;
                if (outcome == ExecutionOutcome.Failure)
                {
                    if (failDoubleLoop)
                    {
                        Core.Error("Detected start of infinite loop, no space for execution fail animation: did you start an animation inside a wall?");
                        return;
                    }

                    anim = AM_DefOf.AM_Execution_Fail;
                    failDoubleLoop = true;
                }
                else
                {
                    // Pick random anim, weighted.
                    anim = possibilities.RandomElementByWeightExcept(d => (fixedAnimationList ? 0.1f : 0f) + d.Probability, except);
                    if (anim == null)
                    {
                        Core.Warn("No possible execution animations after reaching target (no space)!");
                        return;
                    }
                }                

                except.Add(anim);

                // Do we have space for this animation?
                ulong animMask = flipX ? anim.FlipClearMask : anim.ClearMask;
                ulong result = animMask & occupiedMask; // The result should be 0.

                if (result == 0)
                {
                    // Do animation promotion, unless using fixed animations.
                    if (!fixedAnimationList)
                    {
                        anim = anim.TryGetPromotionDef(new AnimDef.PromotionInput
                        {
                            Attacker = toil.actor,
                            FlipX = flipX,
                            OccupiedMask = occupiedMask,
                            OriginalAnim = anim,
                            Outcome = outcome,
                            ReqInput = new ReqInput(weaponDef),
                            Victim = Target
                        }) ?? anim;
                    }

                    // Can do the animation!
                    var args = new AnimationStartParameters(anim, toil.actor, Target)
                    {
                        ExecutionOutcome = outcome,
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