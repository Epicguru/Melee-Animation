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
[HarmonyPatch(typeof(ThingComp_CombatAI), nameof(ThingComp_CombatAI.OnScanFinished))]
public static class ThingComp_CombatAI_OnScanFinished_Transpiler
{
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var isDeadOrDownedGetter = AccessTools.PropertyGetter(typeof(ThingComp_CombatAI), nameof(ThingComp_CombatAI.IsDeadOrDowned));
        if (isDeadOrDownedGetter == null)
            throw new System.Exception("ThingComp_CombatAI.IsDeadOrDowned method not found.");

        var list = instructions.ToList();
        bool found = false;

        for (int i = 1; i < list.Count; i++)
        {
            var ins = list[i];

            if (ins.Calls(isDeadOrDownedGetter))
            {
                found = true;
                list[i] = MakeReplacement();
                break;
            }
        }

        if (!found)
            Core.Error("Failed to find reference instruction for OnScanFinished transpiler! CAI compatibility will be broken.");

        return list.AsEnumerable();
    }

    private static CodeInstruction MakeReplacement()
    {
        var detour = AccessTools.Method(typeof(ThingComp_CombatAI_OnScanFinished_Transpiler), nameof(ShouldExitEarly));
        if (detour == null)
            throw new System.Exception("ShouldExitEarly method not found.");

        return new CodeInstruction(OpCodes.Call, detour);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool ShouldExitEarly(ThingComp_CombatAI comp)
    {
        return comp.IsDeadOrDowned || (comp.parent is Pawn p && p.TryGetAnimator() != null);
    }
}