using AM.Grappling;
using Verse;

namespace AM.Events.Workers;

internal class PuntPawnWorker : EventWorkerBase
{
    public override string EventID => "PuntPawn";

    public override void Run(AnimEventInput input)
    {
        var e = input.Event as PuntPawnEvent;
        Pawn target = input.GetPawnFromIndex(e.PawnIndex);

        if (target == null)
        {
            Core.Warn($"Failed to get target pawn for Punt event, index {e.PawnIndex}");
            return;
        }

        // Determine direction.
        bool right = e.Right;
        if (input.Animator.MirrorHorizontal)
            right = !right;

        // Add the pawn to the list of special pawns that are allowed to be despawned during the animation.
        input.Animator.FlagAsValidIfDespawned(target);

        // Get range from def.
        int range = input.Animator.Def.TryGetAdditionalData("PuntRange", 10);

        // Get dest cell.
        IntVec3 dest = KnockbackFlyer.GetEndCell(target, right ? IntVec3.East : IntVec3.West, range);

        // Spawn flyer.
        var flyer = KnockbackFlyer.MakeKnockbackFlyer(target, dest);

        if (flyer == null)
            Core.Error($"Failed to spawn knockback flyer for pawn {target}!");
    }
}