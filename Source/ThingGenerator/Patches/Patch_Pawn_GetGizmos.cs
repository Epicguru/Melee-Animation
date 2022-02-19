using System;
using HarmonyLib;
using System.Collections.Generic;
using AAM.Gizmos;
using Verse;

namespace AAM.Patches
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public class Patch_Pawn_GetGizmos
    {
        static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, Pawn __instance)
        {
            AnimationGizmo gizmo = null;
            try
            {
                if (ShouldShowFor(__instance))
                    gizmo = new AnimationGizmo(__instance);
            }
            catch (Exception e)
            {
                Core.Error("Exception in gizmo patch", e);
            }

            if (gizmo != null)
                yield return gizmo;

            foreach (var g in values)
                yield return g;
        }

        private static bool ShouldShowFor(Pawn pawn)
        {
            if (!Core.Settings.ShowMultipleGizmos && Find.Selector.SelectedPawns.Count > 1)
                return false;

            return !pawn.Dead && !pawn.Downed && pawn.RaceProps.Humanlike && (Prefs.DevMode || pawn.IsColonistPlayerControlled) && (Core.Settings.ShowGizmosWithoutMeleeWeapon || pawn.GetFirstMeleeWeapon() != null);
        }
    }
}
