using Verse;
using Verse.AI;

namespace AM.Jobs;

public static class ToilUtils
{
    public static Toil DoAnimationToil(AnimationStartParameters args)
    {
        var toil = ToilMaker.MakeToil();
        toil.defaultCompleteMode = ToilCompleteMode.Never;
        toil.socialMode = RimWorld.RandomSocialMode.Off;

        toil.initAction = () =>
        {
            if (!args.TryTrigger(out var animator))
            {
                // Failed to start animation, end job.
                toil.actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                Core.Error($"Failed to start animation '{args}' from toil.");
                return;
            }

            // Verify that the toil actor is caught up in the newly created animation.
            var found = toil.actor.TryGetAnimator();
            if (animator == found)
                return;
            toil.actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
            Core.Error($"Animation was started for toil but {toil.actor} was not registered correctly to the newly created animation.");
        };

        // Tick:
        toil.tickAction = () =>
        {
            // Check if the animation has finished:
            var anim = toil.actor?.TryGetAnimator();
            if (anim == null || anim.IsDestroyed)
            {
                toil.actor.jobs.curDriver.ReadyForNextToil();
                return;
            }
        };

        return toil;
    }
}
