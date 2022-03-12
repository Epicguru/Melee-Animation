using CompSlotLoadable;
using SWSaber;
using UnityEngine;
using Verse;

namespace AAM.LightsaberPatch
{
    public static class Utils
    {
        public static bool CanActivateLightsaber(ThingWithComps saber)
        {
            if (saber == null)
                return false;

            var saberComp = saber.TryGetComp<CompLightsaberActivatableEffect>();
            return CanActivateLightsaber(saberComp);
        }

        public static bool CanActivateLightsaber(CompLightsaberActivatableEffect comp)
        {
            if (comp == null)
                return false;

            return comp.CanActivate();
        }

        public static Color? TryGetLightsaberColor(ThingWithComps saber) => TryGetLightsaberColor(saber?.TryGetComp<CompLightsaberActivatableEffect>());

        public static Color? TryGetLightsaberColor(CompLightsaberActivatableEffect comp)
        {
            if (comp == null)
                return null;

            var slots = comp.parent.TryGetComp<CompSlotLoadable.CompSlotLoadable>();
            if (slots == null)
                return null;

            var crystalSlot = slots.Slots.FirstOrFallback(x => (x.def as SlotLoadableDef)?.doesChangeColor ?? false);
            if (crystalSlot == null)
                return null;

            var bonusComp = crystalSlot.SlotOccupant?.TryGetComp<CompSlottedBonus>();
            if (bonusComp == null)
                return null;

            return bonusComp.Props.color;
        }
    }
}
