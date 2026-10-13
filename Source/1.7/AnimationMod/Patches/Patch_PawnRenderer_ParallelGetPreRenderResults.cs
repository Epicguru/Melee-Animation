using AM.Idle;
using HarmonyLib;
using JetBrains.Annotations;
using Verse;

namespace AM.Patches;

/// <summary>
/// This is used to add a position and rotation offset when fighting in melee.
/// Using a full animator for this is not necessary or desirable, so this is a separate patch and it makes things simpler.
/// </summary>
[HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.ParallelGetPreRenderResults))]
[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public static class Patch_PawnRenderer_ParallelGetPreRenderResults
{
    [HarmonyPriority(Priority.Last)]
    public static void Postfix(PawnRenderer __instance, ref PawnRenderer.PreRenderResults __result)
    {
        var pawn = __instance.pawn;
        var idleComp = pawn?.TryGetComp<IdleControllerComp>();
        idleComp?.AddBodyDrawOffset(ref __result);
    }
}