using Verse;
using Verse.AI;

namespace AM.Jobs;

public class JobDriver_GoToDuelSpot : JobDriver_GoToAnimationSpot
{
    protected override Toil MakeEndToil()
    {
#if V13
        var toil = new Toil();
#else
        var toil = ToilMaker.MakeToil();
#endif

        toil.initAction = () =>
        {
            StartDuel(toil.actor, Target);
        };

        return toil;
    }

    private void StartDuel(Pawn self, Pawn target)
    {
        static bool IsValid(Pawn p) => p is { Dead: false, Downed: false } && p.TryGetAnimator() == null;

        if (!IsValid(self))
        {
            Core.Error($"Pawn {self} is invalid when attempting to start a duel.");
            return;
        }
        if (!IsValid(target))
        {
            Core.Error($"Pawn {target} is invalid when attempting to start a duel.");
            return;
        }

        // Flip X is random.
        // This is to make the duel animation have more variety instead of having the left/right pawn
        // always do the same moves.
        bool flipX = Rand.Bool;
        bool targetIsRight = Target.Position.x > self.Position.x;
        Pawn leftPawn = targetIsRight ? self : target;
        Pawn rightPawn = targetIsRight ? target : self;
        Pawn main = flipX ? rightPawn : leftPawn;
        Pawn other = flipX ? leftPawn : rightPawn;

        AnimDef duelAnim = AnimDef.GetDefsOfType(AnimType.Duel).RandomElementByWeightWithFallback(d => d.Probability);
        if (duelAnim == null)
        {
            Core.Error("Failed to find any duel animation to play! This is probably because the user disabled all duel animations in the settings.");
            return;
        }

        var startArgs = new AnimationStartParameters(duelAnim, main, other)
        {
            FlipX = flipX,
        };

        if (!startArgs.TryTrigger())
            Core.Error($"Failed to start duel animation between {main} and {other} after walking!");
    }
}