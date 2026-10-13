using JetBrains.Annotations;
using Verse;

namespace AM.Health;

/// <summary>
/// Makes it so that the hediff will be removed instantly the first time it is tended.
/// </summary>
[UsedImplicitly]
public class HediffComp_SingleTendRemove : HediffComp
{
    public override bool CompShouldRemove => base.CompShouldRemove || parent.IsTended();
}
