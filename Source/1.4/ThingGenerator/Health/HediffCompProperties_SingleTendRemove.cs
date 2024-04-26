using JetBrains.Annotations;
using Verse;

namespace AM.Health;

[UsedImplicitly]
public class HediffCompProperties_SingleTendRemove : HediffCompProperties
{
    public HediffCompProperties_SingleTendRemove()
    {
        base.compClass = typeof(HediffComp_SingleTendRemove);
    }
}
