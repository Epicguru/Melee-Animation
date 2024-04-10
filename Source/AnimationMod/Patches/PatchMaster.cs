using Verse;

namespace AM.Patches;

public static class PatchMaster
{
    private static Pawn lastPawn;
    private static AnimRenderer lastRenderer;

    public static AnimRenderer GetAnimator(Pawn pawn)
    {
        if (pawn == lastPawn && lastRenderer is { IsDestroyed: false })
            return lastRenderer;

        lastPawn = pawn;
        lastRenderer = AnimRenderer.TryGetAnimator(pawn);
        return lastRenderer;
    }
}
