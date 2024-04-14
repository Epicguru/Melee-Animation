namespace AM.PromotionWorkers;

public sealed class MustHaveHeadPromotionWorker : AnimDef.IPromotionWorker
{
    public float GetPromotionRelativeChanceFor(in AnimDef.PromotionInput input)
    {
        bool hasHead = input.Victim.RaceProps?.Humanlike ?? false;
        return hasHead ? 1 : 0;
    }
}