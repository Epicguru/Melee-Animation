using HarmonyLib;
using System.Collections.Generic;
using AAM.Gizmos;
using Verse;

namespace AAM.Patches
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public class Patch_Pawn_GetGizmos
    {
        static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values)
        {
            foreach (var gizmo in values)
                yield return gizmo;

            yield return new AnimationGizmo();
        }
    }
}
