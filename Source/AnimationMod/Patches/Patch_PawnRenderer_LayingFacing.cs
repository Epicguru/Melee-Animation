using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace AM.Patches;

/// <summary>
/// Only used to override corpse direction.
/// </summary>
[HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.LayingFacing))]
public static class Patch_PawnRenderer_LayingFacing
{
    public static readonly Dictionary<Pawn, Rot4> OverrideRotations = new Dictionary<Pawn, Rot4>();

    public static void Tick()
    {
        if (GenTicks.TicksGame % (60 * 30) == 0)
            OverrideRotations.RemoveAll(p => !p.Key.SpawnedOrAnyParentSpawned);
    }

    static void Postfix(Pawn ___pawn, ref Rot4 __result)
    {
        if (!___pawn.Dead)
            return;

        if (!OverrideRotations.TryGetValue(___pawn, out var found))
            return;

        __result = found;
    }
}