using CombatAI.Comps;
using HarmonyLib;
using JetBrains.Annotations;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Verse;

namespace AM.CAI5000Patch;

[UsedImplicitly]
[HarmonyPatch(typeof(ThingComp_CombatAI), nameof(ThingComp_CombatAI.CompTickRare))]
public static class ThingComp_CombatAI_CompTickRare_Transpiler
{
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var spawnedGetter = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Spawned));
        if (spawnedGetter == null)
            throw new System.Exception("Thing.Spawned method not found.");

        var list = instructions.ToList();
        bool found = false;

        for (int i = 1; i < list.Count; i++)
        {
            var ins = list[i];

            if (ins.Calls(spawnedGetter))
            {
                found = true;
                list[i] = MakeReplacement();
                break;
            }
        }

        if (!found)
            Core.Error("Failed to find reference instruction for CompTickRare transpiler! CAI compatibility will be broken.");

        return list.AsEnumerable();
    }

    private static CodeInstruction MakeReplacement()
    {
        var detour = AccessTools.Method(typeof(ThingComp_CombatAI_CompTickRare_Transpiler), nameof(ShouldKeepGoing));
        if (detour == null)
            throw new System.Exception("ShouldKeepGoing method not found.");

        return new CodeInstruction(OpCodes.Call, detour);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool ShouldKeepGoing(Pawn pawn)
    {
        return pawn.Spawned && pawn.TryGetAnimator() == null;
    }
}