using AM.Grappling;
using Verse;
using Verse.AI;

namespace AM.UniqueSkills;

public abstract class ChanneledUniqueSkillInstance : UniqueSkillInstance
{
    public int TickLastTriggered = -1000000;

    protected Pawn CurrentTarget;

    public float GetCooldownSecondsLeft()
    {
        int ticksSinceLast = GenTicks.TicksGame - TickLastTriggered;
        return (Def.baseCooldownTicks - ticksSinceLast) / 60f;
    }

    public override string CanTriggerOn(in LocalTargetInfo target)
    {
        // Already channeling.
        if (CurrentTarget != null)
            return "AM.Skill.AlreadyActive".Translate();

        // Skill cooldown.
        float cooldownRemaining = GetCooldownSecondsLeft();
        if (cooldownRemaining > 0)
            return "AM.Skill.OnCooldown".Translate(cooldownRemaining.ToString("F1"));

        // Check not dead, downed etc.
        var pawn = target.Pawn;
        if (pawn == null || pawn.Downed || pawn.Dead || !pawn.Spawned || pawn.IsInAnimation() || GrabUtility.IsBeingTargetedForGrapple(pawn))
            return "AM.Skill.BadTargetState".Translate();

        return null;
    }

    public override void Tick()
    {
        base.Tick();

        if (CurrentTarget == null)
            return;

        var animator = CurrentTarget.TryGetAnimator();
        if (animator == null || animator.IsDestroyed)
        {
            // Animation has ended.
            CurrentTarget = null;
            Core.Error("Animator was null or destroyed but the OnEnd callback did not trigger. Probably due to save-reload.");
        }
        else
        {
            if (Pawn.Downed || Pawn.Dead || !Pawn.Spawned)
            {
                Core.Warn("Casting pawn is dead, downed or despawned, cancelling the channeled animation.");
                animator.Destroy();
                CurrentTarget = null;
            }
        }
    }

    protected virtual AnimationStartParameters MakeAnimStartParams(Pawn target)
    {
        return new AnimationStartParameters(Def.animation, target)
        {
            ExecutionOutcome = ExecutionOutcome.Kill
        };
    }

    public override bool TryTrigger(in LocalTargetInfo target)
    {
        // Try start animation on target.
        var args = MakeAnimStartParams(target.Pawn);
        if (!args.TryTrigger(out var animator))
        {
            Core.Error($"Failed to start channeled skill animation {args.Animation} on {target.Pawn}!");
            return false;
        }
        // Used as the 'killer' in animation event.
        animator.NonAnimatedPawns.Add(Pawn);

        // Cancel all target verbs.
        if (Pawn.verbTracker?.AllVerbs != null)
            foreach (var verb in Pawn.verbTracker.AllVerbs)
                verb.Reset();
        if (Pawn.equipment?.AllEquipmentVerbs != null)
            foreach (var verb in Pawn.equipment.AllEquipmentVerbs)
                verb.Reset();

        // Start channel job.
        var newJob = JobMaker.MakeJob(AM_DefOf.AM_ChannelAnimation, target);
        Pawn.jobs.StartJob(newJob, JobCondition.InterruptForced);

        if (Pawn.CurJobDef != AM_DefOf.AM_ChannelAnimation)
        {
            Core.Error($"Failed to start channel animation job on pawn {Pawn}. Mod conflict?");
            animator.Destroy();
            return false;
        }

        TickLastTriggered = GenTicks.TicksGame;
        animator.OnEndAction += OnEnd;
        CurrentTarget = target.Pawn;
        OnAnimationStart(animator);
        return true;
    }

    private void OnEnd(AnimRenderer anim)
    {
        Core.Log($"Anim ended. Was interrupted: {anim.WasInterrupted}");
        anim.OnEndAction -= OnEnd;

        try
        {
            OnAnimationComplete(!anim.WasInterrupted);
        }
        finally
        {
            CurrentTarget = null;
        }
    }

    public abstract void OnAnimationStart(AnimRenderer animator);

    public abstract void OnAnimationComplete(bool didRunToEnd);

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref TickLastTriggered, "tickLastTriggered", -1000000);
        Scribe_References.Look(ref CurrentTarget, "currentTarget");
    }
}
