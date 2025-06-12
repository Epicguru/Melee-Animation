using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;

namespace AM.Patches;

/// <summary>
/// The proximity detector freaks out if it detects any invisible pawn in range,
/// which includes the animated pawns.
/// This patch makes them not be considered invisible when the detection process is running.
/// </summary>
[HarmonyPatch(typeof(Building_ProximityDetector), nameof(Building_ProximityDetector.RunDetection))]
[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.Itself | ImplicitUseTargetFlags.WithMembers)]
public class Patch_Building_ProximityDetector_RunDetection
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(ref bool __state)
    {
        __state = Patch_InvisibilityUtility_IsPsychologicallyInvisible.IsRendering;
        Patch_InvisibilityUtility_IsPsychologicallyInvisible.IsRendering = true;
    }

    [HarmonyPriority(Priority.First)]
    private static void Postfix(bool __state)
    {
        Patch_InvisibilityUtility_IsPsychologicallyInvisible.IsRendering = __state;
    }
}