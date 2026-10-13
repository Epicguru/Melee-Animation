using Verse;

namespace AM.Patches;

public static class PatchMaster
{
    private static readonly object key = new object();

    private static Pawn lastPawn;
    private static AnimRenderer lastRenderer;

    public static AnimRenderer GetAnimator(Pawn pawn)
    {
        lock (key)
        {
            if (pawn == lastPawn && lastRenderer is { IsDestroyed: false })
                return lastRenderer;

            lastPawn = pawn;
            lastRenderer = AnimRenderer.TryGetAnimator(pawn);
        }

        return lastRenderer;
    }
}
