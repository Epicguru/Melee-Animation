using System.Linq;
using CompSlotLoadable;
using SWSaber;
using UnityEngine;
using Verse;

namespace AM.LightsaberPatch;

public static class Extensions
{
    public static Color? TryGetLightsaberColor(this Thing lightsaber)
    {
        var comp = lightsaber.TryGetComp<CompCrystalSlotLoadable>();
        if (comp == null)
            return null;

        var found = (from t in comp.Slots
                    let oc = t.SlotOccupant
                    where oc is not null
                    let effect = oc.TryGetComp<CompSlottedBonus>()
                    where effect is not null
                    select effect.Props.color).FirstOrDefault();

        if (found == default)
            return null;

        return found;
    }
}