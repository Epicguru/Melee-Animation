using System;
using System.Collections.Generic;
using System.Linq;
using AM.Controller;
using AM.Controller.Requests;
using AM.Grappling;
using AM.Reqs;
using RimWorld;
using Verse;
using Verse.AI;

namespace AM.Jobs;

/*
 * Target A: Duel opponent pawn.
 * Target B: Target standing spot in duel.
 * Target C: Standing spot object itself.
 */
public class JobDriver_DoFriendlyDuel : JobDriver, IDuelEndNotificationReceiver
{
    private static readonly ActionController controller = new ActionController();

    // Wait for up to a minute for the opponent to arrive.
    public const int MAX_WAIT_TICKS = 60 * 60;

    private bool ShouldFaceEast => TargetThingC.Position == TargetB.Cell;

    private int ticksSpentWaiting;
    private bool didWin;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        // Attempt to reserve destination cell.
        return pawn.Reserve(job.GetTarget(TargetIndex.B), job, errorOnFailed: errorOnFailed);
    }

    public override IEnumerable<Toil> MakeNewToils()
    {
        // Run! No time to waste!
        job.locomotionUrgency = LocomotionUrgency.Sprint;

        // Several important checks including melee weapons, spawn status etc.
        AddFailCondition(ShouldJobFail);
        // Opponent mental state check.
        this.FailOnAggroMentalState(TargetIndex.A);
        // Duel spot cannot be forbidden.
        this.FailOnDespawnedNullOrForbidden(TargetIndex.C);


        // Reserve duel spot:
        yield return Toils_Reserve.ReserveDestination(TargetIndex.B);

        // Walk to duel spot:
        yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);

        // Wait for opponent to arrive.
        yield return WaitForOpponent();

        // Small pause.
        yield return Toils_General.Wait(30, TargetIndex.A);

        // Bow to the opponent:
        yield return ToilUtils.DoAnimationToil(MakeBowStartParams);

        // Wait for the opponent to finish bowing:
        yield return WaitUntilOtherBowFinished();

        // Start the duel:
        yield return StartMainDuelAnimation();

        // Wait for the duel to end:
        yield return WaitForDuelToEnd();

        // End actions (mood, opinion etc.):
        yield return DoEndActions();
    }

    private Toil StartMainDuelAnimation()
    {
        var toil = ToilMaker.MakeToil();
        toil.defaultCompleteMode = ToilCompleteMode.Delay;
        toil.defaultDuration = 1; // Wait a tick to be very sure that the animation has started.

        // Only 1 pawn needs to start the animation.
        // The other should simply wait a couple of frames for the animation to start and be registered.

        toil.initAction = () => StartDuelInitAction(toil);

        return toil;
    }

    private void StartDuelInitAction(Toil toil)
    {
        // Who should start the animation?
        // It doesn't actually really matter, as long as it's only 1 of the pawns.
        var self = toil.actor;
        var other = toil.actor.jobs.curJob.targetA.Pawn;

        bool shouldStart = string.Compare(self.ThingID, other.ThingID, StringComparison.Ordinal) > 0;
        if (!shouldStart)
            return;

        var req = new DuelAttemptRequest
        {
            A = self,
            B = other,
            NoErrorMessages = false
        };

        // Make duel report so ensure that it can be started:
        var report = controller.GetDuelReport(req);
        if (!report.CanStartDuel || report.MustWalk)
        {
            Core.Error($"Controller says that the duel animation cannot be started right now: {report.ErrorMessage}");
            EndJobWith(JobCondition.Incompletable);
            return;
        }

        // Start animation using various flip options depending on the report.
        if (report.CenteredOnPawn == null)
        {
            if (Rand.Chance(0.5f))
                StartAnimationFromPerspective(report.DuelAnimation, self, other);
            else
                StartAnimationFromPerspective(report.DuelAnimation, other, self);
        }
        else
        {
            StartAnimationFromPerspective(report.DuelAnimation, report.CenteredOnPawn, report.CenteredOnPawn == self ? other : self);
        }
    }

    private void StartAnimationFromPerspective(AnimDef def, Pawn main, Pawn second)
    {
        Core.Log($"Starting friendly duel animation from the perspective of {main}");
        bool fx = main.Position.x > second.Position.x;

        var startArgs = new AnimationStartParameters(def, main, second)
        {
            CustomJobDef = AM_DefOf.AM_DoFriendlyDuel,
            FlipX = fx
        };

        if (!startArgs.TryTrigger(out var animator))
        {
            Core.Error("Failed to start duel animation (Trigger())");
            EndJobWith(JobCondition.Incompletable);
            return;
        }

        // Flag duel as friendly so the victim doesn't get executed.
        animator.IsFriendlyDuel = true;

        // Set duel cooldowns.
        main.GetMeleeData().TimeSinceFriendlyDueled = 0;
        second.GetMeleeData().TimeSinceFriendlyDueled = 0;
    }

    private Toil WaitForDuelToEnd()
    {
        var toil = ToilMaker.MakeToil();
        toil.defaultCompleteMode = ToilCompleteMode.Never;

        toil.initAction = () =>
        {
            ticksSpentWaiting = 0;
        };

        toil.tickAction = () =>
        {
            if (toil.actor.TryGetAnimator() == null)
            {
                toil.actor.jobs.curDriver.ReadyForNextToil();
                return;
            }

            // Gain joy and recreation.
            if (pawn.needs.joy != null)
                JoyUtility.JoyTickCheckEnd(pawn, 1, JoyTickFullJoyAction.None);

            // Duels shouldn't last over 2 minutes. Sanity check...
            if (ticksSpentWaiting > (120f * 60f) / Core.Settings.GlobalAnimationSpeed)
            {
                Core.Error("Ran out of time waiting for opponent to finish bowing! Max wait time is 4 seconds.");
                EndJobWith(JobCondition.Incompletable);
            }
        };

        return toil;
    }

    private Toil DoEndActions()
    {
        var toil = ToilMaker.MakeToil();
        toil.defaultCompleteMode = ToilCompleteMode.Instant;

        toil.initAction = () =>
        {
            Core.Log("Duel ended, applying moods.");

            // Give mood offsets to both pawns (use didWin to only apply once).
            var winner = didWin ? pawn : TargetA.Pawn;
            var loser = !didWin ? pawn : TargetA.Pawn;
            ActionController.TryGiveDuelThoughts(winner, loser, true);
        };

        return toil;
    }

    public void Notify_OnDuelEnd(bool isWinner)
    {
        didWin = isWinner;
    }

    private Toil WaitForOpponent()
    {
        var toil = ToilMaker.MakeToil();

        // Handles facing, look towards the enemy side.
        toil.handlingFacing = true;

        toil.tickAction = () =>
        {
            // Make the pawn face the opponent's duel spot while they wait.
            toil.actor.Rotation = ShouldFaceEast ? Rot4.East : Rot4.West;

            // Stop waiting after a while.
            ticksSpentWaiting++;
            if (ticksSpentWaiting > MAX_WAIT_TICKS)
            {
                EndJobWith(JobCondition.Incompletable);
                string txt = "AM.Error.Duel.WaitedTooLong".Translate(toil.actor, job.targetA.Pawn);
                Messages.Message(txt, MessageTypeDefOf.NegativeEvent);
                return;
            }

            // Check if opponent is at cell.
            var op = TargetA.Pawn;
            var dest = TargetC.Cell == toil.actor.Position ? TargetC.Cell + new IntVec3(1, 0, 0) : TargetC.Cell;
            if (op.Position == dest && !op.pather.Moving)
            {
                ReadyForNextToil();
            }
        };

        // Just wait until I say so!
        toil.defaultCompleteMode = ToilCompleteMode.Never;

        return toil;
    }

    private Toil WaitUntilOtherBowFinished()
    {
        var toil = ToilMaker.MakeToil();
        toil.defaultCompleteMode = ToilCompleteMode.Never;
        toil.initAction = () =>
        {
            ticksSpentWaiting = 0;
        };

        toil.tickAction = () =>
        {
            var opponent = toil.actor.jobs.curJob.targetA.Pawn;
            if (opponent is not { Spawned: true, Dead: false, Downed: false })
            {
                Core.Error("Opponent went missing when waiting for them to finish bowing!");
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            // If the enemy is not in an animation, we are good to go.
            var animator = opponent.TryGetAnimator();
            if (animator == null || animator.IsDestroyed || animator.Def.type != AnimType.DuelBow)
            {
                ReadyForNextToil();
                return;
            }

            ticksSpentWaiting++;
            if (ticksSpentWaiting > 240f / Core.Settings.GlobalAnimationSpeed)
            {
                Core.Error("Ran out of time waiting for opponent to finish bowing! Max wait time is 4 seconds.");
                EndJobWith(JobCondition.Incompletable);
            }
        };

        return toil;
    }

    private AnimationStartParameters MakeBowStartParams(Toil toil)
    {
        // Get bow animation.
        var anim = GetBowAnimationDef(toil.actor.GetFirstMeleeWeapon());
        if (anim == null)
        {
            Core.Error("Failed to find bow animation.");
            return default;
        }

        // Make args.
        bool flipX = !ShouldFaceEast;
        return new AnimationStartParameters(anim, toil.actor)
        {
            CustomJobDef = AM_DefOf.AM_DoFriendlyDuel,
            FlipX = flipX,
        };
    }

    private static AnimDef GetBowAnimationDef(Thing weapon)
    {
        if (weapon == null)
            return null;

        var req = new ReqInput(weapon.def);
        var options = AnimDef.GetDefsOfType(AnimType.DuelBow).Where(d => d.Probability > 0 && d.Allows(req));
        return options.RandomElementByWeightWithFallback(d => d.Probability);
    }

    private bool ShouldJobFail()
    {
        bool IsPawnValid(Pawn p)
        {
            if (p == null || p.Dead || p.Downed || !p.Spawned)
                return false;

            if (p.InMentalState)
                return false;

            var animator = p.TryGetAnimator();
            if (animator != null && animator.Def.type is not (AnimType.DuelBow or AnimType.Duel) && animator.Def != AM_DefOf.AM_Duel_WinFriendlyDuel && animator.Def != AM_DefOf.AM_Duel_WinFriendlyDuel_Reject)
                return false;

            if (GrabUtility.IsBeingTargetedForGrapple(p))
                return false;

            if (p.GetFirstMeleeWeapon() == null)
                return false;
            
            if (p != pawn && Find.TickManager.TicksGame - startTick > 3)
            {
                if (p.jobs?.curDriver is not JobDriver_DoFriendlyDuel otherDriver)
                    return false;

                if (otherDriver.TargetA.Pawn != pawn)
                    return false;
            }

            return true;
        }

        if (!IsPawnValid(pawn))
            return true;
        if (!IsPawnValid(TargetThingA as Pawn))
            return true;

        return false;
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref ticksSpentWaiting, "ticksSpentWaiting");
        Scribe_Values.Look(ref didWin, "didWin");
    }
}
